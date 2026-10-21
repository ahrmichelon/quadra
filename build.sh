#!/bin/bash
# Builds Quadra and assembles Quadra.app (universal: Apple silicon + Intel).
#
#   ./build.sh            build only
#   ./build.sh --install  build, copy to /Applications and open
#   ./build.sh --zip      build and create Quadra.zip (for a release)
#   ./build.sh --test     run the tests only
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

OUT="build"
SCRATCH=()
if mount | grep -Eq "^/dev/$(stat -f %Sd .) .*\((exfat|msdos|ntfs)"; then
  OUT="$HOME/Library/Caches/Quadra"
  SCRATCH=(--scratch-path "$OUT/.build")
  # Remove the "._" files macOS left next to the sources, or Swift tries to compile them.
  find . -name '._*' -type f -not -path './.git/*' -delete
  rm -rf .build
fi
swift_() { swift "$@" ${SCRATCH[@]+"${SCRATCH[@]}"}; }

if [[ "${1:-}" == "--test" ]]; then
  swift_ test
  exit
fi

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

IDENTITY="${QUADRA_SIGN_IDENTITY:-$(security find-identity -v -p codesigning 2>/dev/null \
  | awk -F'"' '/Developer ID Application|Apple Development/ { print $2; exit }')}"
SIGN=(--force --timestamp=none --sign "${IDENTITY:--}")
# The release workflow keeps its certificate in a keychain of its own.
[[ -n "${QUADRA_SIGN_KEYCHAIN:-}" ]] && SIGN+=(--keychain "$QUADRA_SIGN_KEYCHAIN")
codesign "${SIGN[@]}" "$APP"
echo "Built $APP (signed with: ${QUADRA_SIGN_NAME:-${IDENTITY:-ad hoc}})"

case "${1:-}" in
  --install)
    osascript -e "tell application id \"$BUNDLE_ID\" to quit" >/dev/null 2>&1 || true
    sleep 1
    pkill -f "/Contents/MacOS/Quadra$" 2>/dev/null || true
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
