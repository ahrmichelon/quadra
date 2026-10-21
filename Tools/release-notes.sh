#!/bin/bash
# Prints the notes for a release: the version's section of CHANGELOG.md plus how to install.
# Usage: Tools/release-notes.sh 1.1.0   (or v1.1.0)
set -euo pipefail
cd "$(dirname "$0")/.."
VERSION="${1#v}"

NOTES="$(awk -v heading="## $VERSION" '$0 == heading { found = 1; next } found && /^## / { exit } found' CHANGELOG.md)"
if [[ -z "${NOTES//[[:space:]]/}" ]]; then
  echo "CHANGELOG.md has no section \"## $VERSION\"" >&2
  exit 1
fi

echo "## What's new"
echo "$NOTES"
cat <<'EOF'

## Install

With Homebrew:

```bash
brew install --cask ahrmichelon/tap/quadra
```

Or download **Quadra.zip** below, unzip it and move **Quadra.app** to **Applications**.
The first time, open **System Settings → Privacy & Security** and click **Open Anyway**,
then allow **Accessibility** when Quadra asks. Requires macOS 14 or later.

Already have Quadra? It tells you about new versions; with Homebrew, run `brew upgrade --cask quadra`.

---

**Em português:** instale com `brew install --cask ahrmichelon/tap/quadra` ou baixe o **Quadra.zip**,
mova o app para **Aplicativos** e, na primeira vez, clique em **Abrir Mesmo Assim** em
Ajustes do Sistema → Privacidade e Segurança. Depois permita a **Acessibilidade**.
EOF
