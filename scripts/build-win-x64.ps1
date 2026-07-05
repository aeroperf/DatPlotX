# Builds and publishes DatPlotX as a self-contained single-file Windows executable.
# Run from the repo root: .\scripts\build-win-x64.ps1
#
# Requires: .NET 10 SDK  (https://dotnet.microsoft.com/download)
# If blocked by execution policy, run once in an elevated PowerShell:
#   Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Read version from Directory.Build.props (the single source of truth)
[xml]$props = Get-Content "Directory.Build.props"
$versionNode = $props.SelectSingleNode('//Version')
if (-not ($versionNode -and $versionNode.InnerText)) {
    throw "No <Version> element found in Directory.Build.props"
}
$VERSION = $versionNode.InnerText
Write-Host "==> Version: $VERSION"

$PUBLISH_DIR = "DatPlotX\bin\Release\net10.0\win-x64\publish"

Write-Host "==> Cleaning previous publish output..."
if (Test-Path $PUBLISH_DIR) {
    Remove-Item -Recurse -Force $PUBLISH_DIR
}

Write-Host "==> Publishing (self-contained, single-file, win-x64)..."
dotnet publish DatPlotX\DatPlotX.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true

Write-Host ""
Write-Host "Done. Executable: $PUBLISH_DIR\DatPlotX.exe"
Write-Host ""
Write-Host "To run:   & `"$PUBLISH_DIR\DatPlotX.exe`""
Write-Host "To zip:   .\scripts\zip-win-release.ps1"
