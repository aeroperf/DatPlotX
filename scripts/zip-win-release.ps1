# Creates a distributable zip of the Windows self-contained executable.
# The zip is written to the repo-root releases/ directory (not inside the
# publish folder) so it cannot be accidentally bundled into future builds.
#
# Run from the repo root: .\scripts\zip-win-release.ps1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Read version from Directory.Build.props (the single source of truth)
[xml]$props = Get-Content "Directory.Build.props"
$versionNode = $props.SelectSingleNode('//Version')
if (-not ($versionNode -and $versionNode.InnerText)) {
    throw "No <Version> element found in Directory.Build.props"
}
$VERSION = $versionNode.InnerText

$PUBLISH_DIR = "DatPlotX\bin\Release\net10.0\win-x64\publish"
$EXE_PATH    = "$PUBLISH_DIR\DatPlotX.exe"
$OUT_DIR     = "releases"
$ZIP_NAME    = "DatPlotX-v${VERSION}-win-x64.zip"
$ZIP_PATH    = "$OUT_DIR\$ZIP_NAME"

if (-not (Test-Path $EXE_PATH)) {
    Write-Error "Executable not found: $EXE_PATH`nRun .\scripts\build-win-x64.ps1 first."
}

if (-not (Test-Path $OUT_DIR)) {
    New-Item -ItemType Directory -Path $OUT_DIR | Out-Null
}

if (Test-Path $ZIP_PATH) {
    Remove-Item -Force $ZIP_PATH
}

Write-Host "==> Creating releases\$ZIP_NAME ..."

# Bundle the exe plus all native DLLs (SkiaSharp, HarfBuzz, Avalonia GL)
# into a single flat zip — these must sit beside the exe at runtime.
$items = Get-ChildItem $PUBLISH_DIR -File | Where-Object { $_.Extension -in '.exe','.dll' }
Compress-Archive -Path $items.FullName -DestinationPath $ZIP_PATH

Write-Host ""
Write-Host "Done. Release zip: $ZIP_PATH"
Get-Item $ZIP_PATH | Select-Object Name, @{N='Size';E={"{0:N0} KB" -f ($_.Length / 1KB)}}
