# Prints the notes for a Windows release: the version's section of Windows/CHANGELOG.md, how to install,
# and the SHA-256 of each .exe.
# Usage: Tools/release-notes.ps1 1.0.0 dist
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Dist
)
$ErrorActionPreference = "Stop"
$changelog = Get-Content (Join-Path $PSScriptRoot ".." "CHANGELOG.md")

$notes = @()
$inside = $false
foreach ($line in $changelog) {
    if ($line -eq "## $Version") { $inside = $true; continue }
    if ($inside -and $line -match '^## ') { break }
    if ($inside) { $notes += $line }
}
if (-not ($notes -join "").Trim()) { throw "Windows/CHANGELOG.md has no section `"## $Version`"" }

"## What's new"
$notes
@"

## Install

Download **Quadra-x64.exe** (most PCs) or **Quadra-arm64.exe** (ARM processors such as Snapdragon), put it
in a folder where it will stay and open it. The first time, Windows may show "Windows protected your PC":
click **More info**, then **Run anyway**. Quadra's icon appears next to the clock (or under the **^** arrow).
Requires Windows 10 (1809 or later) or Windows 11, 64-bit. Nothing else to install.

**Em português:** baixe o **Quadra-x64.exe** (a maioria dos PCs) ou o **Quadra-arm64.exe** (processadores ARM),
coloque numa pasta onde ele vai ficar e abra. Se aparecer "O Windows protegeu o computador", clique em
**Mais informações** e depois em **Executar assim mesmo**. O ícone fica ao lado do relógio (ou na setinha **^**).

## SHA-256

| File | SHA-256 |
|---|---|
"@
Get-ChildItem $Dist -Filter *.exe | Sort-Object Name | ForEach-Object {
    "| $($_.Name) | ``$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())`` |"
}
