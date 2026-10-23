# Writes the winget manifests for a Quadra for Windows release, in the layout of microsoft/winget-pkgs
# (manifests/v/Vulpini/Quadra/<version>/). The release workflow runs it and keeps the files as an artifact;
# a pull request to microsoft/winget-pkgs with them publishes the version on winget.
# Usage: Tools/winget-manifests.ps1 1.3.0 dist out
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Dist,
    [Parameter(Mandatory)] [string] $Out
)
$ErrorActionPreference = "Stop"

$id = "Vulpini.Quadra"
$schema = "1.10.0"  # understood by every winget from 1.10 on (the GitHub runner has 1.11)
$repo = "https://github.com/vulpini-br/quadra"
$tag = "windows-v$Version"
$folder = Join-Path $Out "manifests" "v" "Vulpini" "Quadra" $Version
New-Item -ItemType Directory -Force $folder | Out-Null

function Sha256([string] $name) {
    (Get-FileHash (Join-Path $Dist $name) -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Write-Manifest([string] $name, [string] $kind, [string] $body) {
    $text = "# Made by Windows/Tools/winget-manifests.ps1 in vulpini-br/quadra`n" +
            "# yaml-language-server: `$schema=https://aka.ms/winget-manifest.$kind.$schema.schema.json`n`n" +
            $body.Replace("`r`n", "`n").Trim() + "`n"
    [System.IO.File]::WriteAllText((Join-Path $folder $name), $text, [System.Text.UTF8Encoding]::new($false))
}

Write-Manifest "$id.yaml" "version" @"
PackageIdentifier: $id
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: $schema
"@

# Quadra.exe installs itself for the current user. --install --silent does it without any window and reports
# the result with the exit code; the entry it writes in Settings → Apps ("Quadra", publisher "Vulpini") is how
# winget finds it afterwards, and its QuietUninstallString is how winget removes it.
Write-Manifest "$id.installer.yaml" "installer" @"
PackageIdentifier: $id
PackageVersion: $Version
Platform:
- Windows.Desktop
MinimumOSVersion: 10.0.17763.0
InstallerType: exe
Scope: user
InstallModes:
- interactive
- silent
- silentWithProgress
InstallerSwitches:
  Silent: --install --silent
  SilentWithProgress: --install --silent
  Interactive: --install
UpgradeBehavior: install
ProductCode: Quadra
ReleaseDate: $(Get-Date -Format 'yyyy-MM-dd')
AppsAndFeaturesEntries:
- DisplayName: Quadra
  Publisher: Vulpini
  ProductCode: Quadra
Installers:
- Architecture: x64
  InstallerUrl: $repo/releases/download/$tag/Quadra-x64.exe
  InstallerSha256: $(Sha256 'Quadra-x64.exe')
- Architecture: arm64
  InstallerUrl: $repo/releases/download/$tag/Quadra-arm64.exe
  InstallerSha256: $(Sha256 'Quadra-arm64.exe')
ManifestType: installer
ManifestVersion: $schema
"@

Write-Manifest "$id.locale.en-US.yaml" "defaultLocale" @"
PackageIdentifier: $id
PackageVersion: $Version
PackageLocale: en-US
Publisher: Vulpini
PublisherUrl: https://github.com/vulpini-br
PublisherSupportUrl: $repo/issues
Author: Vulpini Contabilidade Ltda
PackageName: Quadra
PackageUrl: $repo/tree/main/Windows
License: MIT
LicenseUrl: $repo/blob/main/LICENSE
Copyright: Copyright (c) 2026 Vulpini Contabilidade Ltda and Quadra contributors
ShortDescription: Arrange your windows on a grid.
Description: >-
  Quadra is a free, open-source window manager. Press a shortcut or click its icon next to the clock, drag
  across a see-through grid, and the focused window moves to that area of the screen. Save layouts with their
  own shortcuts, send windows to another display, and use any grid from 1 to 20 columns and rows. Works with
  several displays, different scalings and ultrawide monitors.
Moniker: quadra
Tags:
- grid
- layout
- multi-monitor
- productivity
- snap
- tiling
- ultrawide
- window-manager
ReleaseNotesUrl: $repo/releases/tag/$tag
ManifestType: defaultLocale
ManifestVersion: $schema
"@

Write-Manifest "$id.locale.pt-BR.yaml" "locale" @"
PackageIdentifier: $id
PackageVersion: $Version
PackageLocale: pt-BR
Publisher: Vulpini
PublisherUrl: https://github.com/vulpini-br
PublisherSupportUrl: $repo/issues
Author: Vulpini Contabilidade Ltda
PackageName: Quadra
PackageUrl: $repo/blob/main/Windows/README.pt-BR.md
License: MIT
LicenseUrl: $repo/blob/main/LICENSE
ShortDescription: Organize suas janelas numa grade.
Description: >-
  O Quadra é um gerenciador de janelas grátis e de código aberto. Aperte um atalho ou clique no ícone ao lado
  do relógio, arraste numa grade translúcida, e a janela em foco vai para aquela área da tela. Salve layouts
  com atalho próprio, mande janelas para outra tela e use grades de 1 a 20 colunas e linhas. Funciona com
  vários monitores, escalas diferentes e monitores ultrawide.
Tags:
- grade
- janelas
- layout
- monitores
- produtividade
- ultrawide
ReleaseNotesUrl: $repo/releases/tag/$tag
ManifestType: locale
ManifestVersion: $schema
"@

Get-ChildItem $folder | ForEach-Object { Write-Output "Wrote $($_.FullName)" }
