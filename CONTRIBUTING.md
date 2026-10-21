# Contributing to Quadra

Thanks for helping! Issues and pull requests are welcome in English or Portuguese.

## Reporting a problem

Open an issue with:

- your macOS version and Mac (Apple silicon or Intel),
- your displays (for example "one ultrawide 3840 × 1080 plus the laptop screen"),
- the app whose window misbehaved, and what you expected to happen.

## Working on the code

```bash
./build.sh --install   # build, copy to /Applications and open
./build.sh --test      # run the tests (or plain `swift test`)
```

If the project is on an exFAT drive, macOS creates hidden `._` files next to every file, which
breaks Swift builds. `build.sh` notices this, removes those files and builds in
`~/Library/Caches/Quadra` instead, so use `./build.sh --test` rather than `swift test` there.
Keeping the project on an APFS drive avoids the problem entirely.

Open `Package.swift` in Xcode to edit and debug. Quadra needs the Accessibility permission. If
your builds are signed locally (no developer certificate), macOS treats every build as a new app
and asks for the permission again.

`Quadra --snapshot image.png [dark]` and `Quadra --snapshot-settings image.png [dark]` draw the
panel and the Settings window to images without opening them, which helps when changing the look.

### Guidelines

- Keep pull requests small and focused on one thing.
- Add or update tests in `Tests/QuadraTests` when you change geometry, layouts or shortcuts.
- Code comments are in English.
- Every text shown to people goes through `String(localized:)` (AppKit) or a `Text("…")` literal
  (SwiftUI). Add its translation to `App/Localization/pt-BR.lproj/Localizable.strings`.

### Adding a language

Copy `App/Localization/pt-BR.lproj` to a folder named after the language (for example
`es.lproj`), translate the right-hand side of each line, and add the language code to
`CFBundleLocalizations` in `App/Info.plist`.

## Ideas to start with

- Translations into more languages.
- Real screenshots and a short GIF for the README.
- Testing with apps that resize oddly, and reporting what you find.
