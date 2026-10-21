#!/bin/bash
# Redraws the Windows icon (src/Quadra/Assets/Quadra.ico) with every size Windows uses.
# Only needs to run again when the drawing in make-icon.swift changes.
# Runs on a Mac: needs Swift (Xcode or the Command Line Tools) and ImageMagick (brew install imagemagick).
set -euo pipefail
cd "$(dirname "$0")/.."

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

swift Tools/make-icon.swift "$WORK"
FILES=()
for s in 16 20 24 32 40 48 64 96 128 256; do FILES+=("$WORK/icon-$s.png"); done
magick "${FILES[@]}" src/Quadra/Assets/Quadra.ico
echo "Done: src/Quadra/Assets/Quadra.ico"
