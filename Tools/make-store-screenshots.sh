#!/bin/bash
# Makes the Mac App Store screenshots in docs/app-store/screenshots (2880 × 1800, English and
# Portuguese), from the App Store version of the app.
set -euo pipefail
cd "$(dirname "$0")/.."

./build.sh --appstore > /dev/null
# build.sh puts it in build/, or in ~/Library/Caches/Quadra when the project is on an exFAT drive.
APP="build/AppStore/Quadra.app"
[[ -d "$APP" ]] || APP="$HOME/Library/Caches/Quadra/AppStore/Quadra.app"
BUNDLE_ID="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$APP/Contents/Info.plist")"
# The sandboxed app can only write inside its own container.
CONTAINER_TMP="$HOME/Library/Containers/$BUNDLE_ID/Data/tmp"
PICTURES="$(mktemp -d)"
trap 'rm -rf "$PICTURES"' EXIT

render() { # name, language, snapshot option, extra words…
  local name="$1" language="$2" option="$3"; shift 3
  QUADRA_SNAPSHOT_SCALE=3 "$APP/Contents/MacOS/Quadra" "$option" "$CONTAINER_TMP/$name.png" "$@" -AppleLanguages "($language)"
  mv "$CONTAINER_TMP/$name.png" "$PICTURES/$name-$language.png"
}
for language in en pt-BR; do
  render panel "$language" --snapshot dark
  render settings "$language" --snapshot-settings
  render workspaces "$language" --snapshot-settings workspaces
  render welcome "$language" --snapshot-welcome
done

echo "Composing:"
swift Tools/store-screenshots.swift "$PICTURES" docs/app-store/screenshots
