#!/bin/bash
# Builds Quadra and assembles build/Quadra.app (universal: Apple silicon + Intel).
#
#   ./build.sh            build only
#   ./build.sh --install  build, copy to /Applications and open
#   ./build.sh --zip      build and create build/Quadra.zip (for a release)
#
# Signing: uses the first "Developer ID Application" or "Apple Development" certificate
# found in the keychain, or QUADRA_SIGN_IDENTITY if set. Without one, the app is signed
# locally ("ad hoc"), and macOS asks for the Accessibility permission again after every build.
set -euo pipefail
cd "$(dirname "$0")"

APP="build/Quadra.app"
BUNDLE_ID="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' App/Info.plist)"
ARCHS=(--arch arm64 --arch x86_64)

swift build -c release "${ARCHS[@]}"
BIN_DIR="$(swift build -c release "${ARCHS[@]}" --show-bin-path)"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN_DIR/Quadra" "$APP/Contents/MacOS/Quadra"
cp App/Info.plist "$APP/Contents/Info.plist"
cp -R App/Localization/*.lproj "$APP/Contents/Resources/"
[[ -f Branding/AppIcon.icns ]] || ./Tools/make-icon.sh
cp Branding/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"

IDENTITY="${QUADRA_SIGN_IDENTITY:-$(security find-identity -v -p codesigning 2>/dev/null \
  | awk -F'"' '/Developer ID Application|Apple Development/ { print $2; exit }')}"
codesign --force --timestamp=none --sign "${IDENTITY:--}" "$APP"
echo "Built $APP (signed with: ${IDENTITY:-ad hoc})"

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
    rm -f build/Quadra.zip
    ditto -c -k --keepParent "$APP" build/Quadra.zip
    echo "Created build/Quadra.zip"
    ;;
esac
