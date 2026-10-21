#!/bin/bash
# Draws the Quadra icon: Branding/AppIcon.icns (used by the app) and Branding/quadra-icon-1024.png.
# Only needs to run again when the drawing in Tools/make-icon.swift changes.
set -euo pipefail
cd "$(dirname "$0")/.."

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

mkdir -p Branding
swift Tools/make-icon.swift "$WORK/Quadra.iconset" Branding/quadra-icon-1024.png
iconutil -c icns "$WORK/Quadra.iconset" -o Branding/AppIcon.icns
echo "Done: Branding/AppIcon.icns and Branding/quadra-icon-1024.png"
