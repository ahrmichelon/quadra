#!/bin/bash
# Builds Quadra and assembles Quadra.app (universal: Apple silicon + Intel).
#
#   ./build.sh             build only
#   ./build.sh --install   build, copy to /Applications and open
#   ./build.sh --zip       build and create Quadra.zip (for a GitHub release)
#   ./build.sh --test      run the tests only
#   ./build.sh --appstore  build the Mac App Store version (see docs/app-store/GUIA.md)
#
# The app lands in build/, except when the project is on an exFAT (or other non-Mac) drive:
# there macOS writes a hidden "._" file next to every file the compiler creates, which breaks
# the build, so everything is built in ~/Library/Caches/Quadra instead. The code stays put.
#
# Signing: uses the first "Developer ID Application" or "Apple Development" certificate
# found in the keychain, or QUADRA_SIGN_IDENTITY if set. Without one, the app is signed
# locally ("ad hoc"), and macOS asks for the Accessibility permission again after every build.
set -euo pipefail
cd "$(dirname "$0")"
MODE="${1:-}"

OUT="build"
SCRATCH=()
if mount | grep -Eq "^/dev/$(stat -f %Sd .) .*\((exfat|msdos|ntfs)"; then
  OUT="$HOME/Library/Caches/Quadra"
  SCRATCH=(--scratch-path "$OUT/.build")
  # Remove the "._" files macOS left next to the sources, or Swift tries to compile them.
  find . -name '._*' -type f -not -path './.git/*' -delete
  rm -rf .build
fi

if [[ "$MODE" == "--test" ]]; then
  swift test ${SCRATCH[@]+"${SCRATCH[@]}"}
  exit
fi

# The App Store version is compiled with APP_STORE (no update check, no donation link),
# with its own build folder so it never mixes with the regular version.
SWIFT_FLAGS=()
if [[ "$MODE" == "--appstore" ]]; then
  SWIFT_FLAGS=(-Xswiftc -DAPP_STORE)
  SCRATCH=(--scratch-path "$OUT/.build-appstore")
  OUT="$OUT/AppStore"
fi
swift_() { swift "$@" ${SCRATCH[@]+"${SCRATCH[@]}"} ${SWIFT_FLAGS[@]+"${SWIFT_FLAGS[@]}"}; }

APP="$OUT/Quadra.app"
BUNDLE_ID="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' App/Info.plist)"
ARCHS=(--arch arm64 --arch x86_64)

swift_ build -c release "${ARCHS[@]}"
BIN_DIR="$(swift_ build -c release "${ARCHS[@]}" --show-bin-path)"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN_DIR/Quadra" "$APP/Contents/MacOS/Quadra"
cp App/Info.plist "$APP/Contents/Info.plist"
cp -R App/Localization/*.lproj "$APP/Contents/Resources/"
[[ -f Branding/AppIcon.icns ]] || ./Tools/make-icon.sh
cp Branding/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"

if [[ "$MODE" == "--appstore" ]]; then
  # The bundle identifier registered in App Store Connect (decided with the Apple account).
  STORE_ID="${QUADRA_APPSTORE_BUNDLE_ID:-br.com.vulpini.quadra}"
  PLIST="$APP/Contents/Info.plist"
  /usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier $STORE_ID" "$PLIST"
  /usr/libexec/PlistBuddy -c "Add :ITSAppUsesNonExemptEncryption bool false" "$PLIST"

  DIST_ID="${QUADRA_APPSTORE_IDENTITY:-$(security find-identity -v -p codesigning 2>/dev/null \
    | awk -F'"' '/Apple Distribution|3rd Party Mac Developer Application/ { print $2; exit }')}"
  INSTALLER_ID="${QUADRA_INSTALLER_IDENTITY:-$(security find-identity -v 2>/dev/null \
    | awk -F'"' '/3rd Party Mac Developer Installer|Mac Installer Distribution/ { print $2; exit }')}"
  PROFILE="${QUADRA_PROVISIONING_PROFILE:-App/AppStore.provisionprofile}"
  ENTITLEMENTS="$OUT/Quadra.entitlements"
  cp App/AppStore.entitlements "$ENTITLEMENTS"

  if [[ -n "$DIST_ID" && -n "$INSTALLER_ID" && -f "$PROFILE" ]]; then
    # For the store: the provisioning profile goes inside the app, and its app identifier
    # and team are added to the entitlements.
    security cms -D -i "$PROFILE" > "$OUT/profile.plist"
    APP_IDENTIFIER="$(/usr/libexec/PlistBuddy -c 'Print :Entitlements:com.apple.application-identifier' "$OUT/profile.plist")"
    TEAM="$(/usr/libexec/PlistBuddy -c 'Print :TeamIdentifier:0' "$OUT/profile.plist")"
    if [[ "$APP_IDENTIFIER" != "$TEAM.$STORE_ID" ]]; then
      echo "The provisioning profile is for $APP_IDENTIFIER, but the app is $TEAM.$STORE_ID." >&2
      echo "Set QUADRA_APPSTORE_BUNDLE_ID or download the right profile." >&2
      exit 1
    fi
    cp "$PROFILE" "$APP/Contents/embedded.provisionprofile"
    /usr/libexec/PlistBuddy -c "Add :com.apple.application-identifier string $APP_IDENTIFIER" "$ENTITLEMENTS"
    /usr/libexec/PlistBuddy -c "Add :com.apple.developer.team-identifier string $TEAM" "$ENTITLEMENTS"
    codesign --force --sign "$DIST_ID" --entitlements "$ENTITLEMENTS" "$APP"
    productbuild --component "$APP" /Applications --sign "$INSTALLER_ID" "$OUT/Quadra.pkg"
    echo "Built $OUT/Quadra.pkg for the App Store (signed with: $DIST_ID)."
    echo "Upload it with the Transporter app, or: xcrun altool --upload-package \"$OUT/Quadra.pkg\" ..."
  else
    # Without the Apple account's certificates: a sandboxed test build to try on this Mac.
    IDENTITY="$(security find-identity -v -p codesigning 2>/dev/null \
      | awk -F'"' '/Apple Development/ { print $2; exit }')"
    codesign --force --timestamp=none --sign "${IDENTITY:--}" --entitlements "$ENTITLEMENTS" "$APP"
    echo "Built $APP: a sandboxed TEST build of the App Store version ($STORE_ID)."
    echo "It can't be uploaded yet: that needs the Apple Distribution and Mac Installer Distribution"
    echo "certificates and $PROFILE (see docs/app-store/GUIA.md)."
  fi
  exit
fi

IDENTITY="${QUADRA_SIGN_IDENTITY:-$(security find-identity -v -p codesigning 2>/dev/null \
  | awk -F'"' '/Developer ID Application|Apple Development/ { print $2; exit }')}"
SIGN=(--force --timestamp=none --sign "${IDENTITY:--}")
# The release workflow keeps its certificate in a keychain of its own.
[[ -n "${QUADRA_SIGN_KEYCHAIN:-}" ]] && SIGN+=(--keychain "$QUADRA_SIGN_KEYCHAIN")
codesign "${SIGN[@]}" "$APP"
echo "Built $APP (signed with: ${QUADRA_SIGN_NAME:-${IDENTITY:-ad hoc}})"

case "$MODE" in
  --install)
    osascript -e "tell application id \"$BUNDLE_ID\" to quit" >/dev/null 2>&1 || true
    sleep 1
    pkill -f "/Applications/Quadra.app/Contents/MacOS/Quadra$" 2>/dev/null || true
    rm -rf /Applications/Quadra.app
    ditto "$APP" /Applications/Quadra.app
    open /Applications/Quadra.app
    echo "Installed /Applications/Quadra.app"
    ;;
  --zip)
    rm -f "$OUT/Quadra.zip"
    ditto -c -k --keepParent "$APP" "$OUT/Quadra.zip"
    echo "Created $OUT/Quadra.zip"
    ;;
esac
