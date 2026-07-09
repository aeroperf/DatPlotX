#!/bin/bash
# Builds and publishes DatPlotX as a self-contained single-file Linux executable.
# Run from the repo root: ./scripts/build-linux.sh [linux-x64|linux-arm64]
#
# Requires: .NET 10 SDK  (https://dotnet.microsoft.com/download)
#
# This cross-compiles cleanly from macOS or Windows — the linux-x64 runtime pack
# and its native .so files come from NuGet, so no Linux host is needed to *build*.
# You do need Linux (or WSL2) to run or package it as an AppImage.
set -e

RID="${1:-linux-x64}"
case "$RID" in
  linux-x64|linux-arm64) ;;
  *) echo "Error: unsupported RID '$RID'. Use linux-x64 or linux-arm64." >&2; exit 1 ;;
esac

# Read version from Directory.Build.props (the single source of truth)
VERSION=$(grep '<Version>' Directory.Build.props 2>/dev/null | sed 's/.*<Version>\(.*\)<\/Version>.*/\1/' | tr -d '[:space:]')
if [ -z "$VERSION" ]; then
  echo "Error: no <Version> element found in Directory.Build.props" >&2
  exit 1
fi
echo "==> Version: $VERSION"
echo "==> Runtime: $RID"

PUBLISH_DIR="DatPlotX/bin/Release/net10.0/$RID/publish"

echo "==> Cleaning previous publish output..."
rm -rf "$PUBLISH_DIR"

echo "==> Publishing (self-contained, single-file, $RID)..."
# PublishSingleFile bundles the managed assemblies into one executable. The
# SkiaSharp / HarfBuzz native .so files stay beside it — IncludeNativeLibrariesForSelfExtract
# is deliberately NOT set, because extracting them to a temp dir on every launch
# is slow and breaks under restrictive /tmp mount options (noexec).
dotnet publish DatPlotX/DatPlotX.csproj \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true

chmod +x "$PUBLISH_DIR/DatPlotX"

echo ""
echo "Done. Executable: $PUBLISH_DIR/DatPlotX"
echo ""
echo "To run (on Linux):  \"$PUBLISH_DIR/DatPlotX\""
echo "To tarball:         ./scripts/tar-linux-release.sh $RID"
