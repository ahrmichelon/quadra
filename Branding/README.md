# Quadra brand

Arrange your Mac's windows on a grid.

## Files

| File | Use |
|---|---|
| `quadra-icon.svg` | The original vector drawing. Use it on websites, documents and slides. |
| `quadra-icon-1024.png` | The icon at 1024 × 1024, with a transparent background. |
| `AppIcon.icns` | The app icon with every macOS size. `build.sh` copies it into `Quadra.app`. |
| `quadra-social-preview.png` | 1280 × 640 card shown when the repository link is shared. |

To redraw the PNG and the `.icns` after changing the drawing in `Tools/make-icon.swift`:

```bash
./Tools/make-icon.sh
```

## The drawing

A 3 × 3 grid with the top-left 2 × 2 block highlighted: a window taking up an area of the
screen. The menu bar shows the same grid in black or white.

## Colors

| Color | Hex | Where |
|---|---|---|
| Light blue | `#3B62F4` | Top of the background |
| Dark blue | `#2845C9` | Bottom of the background |
| Cyan | `#7FE3FF` | Highlighted block |
| White 28% | `#FFFFFF` at 28% opacity | Other cells |
