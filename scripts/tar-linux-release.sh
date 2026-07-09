#!/bin/bash
# Creates a distributable tar.gz of the Linux self-contained executable.
# The archive is written to the repo-root releases/ directory (not inside the
# publish folder) so it cannot be accidentally bundled into future builds.
#
# tar (not zip) because tar preserves the executable permission bit; a zip
# would land on the user's machine needing a chmod +x.
#
# Run from the repo root: ./scripts/tar-linux-release.sh [linux-x64|linux-arm64]
set -e

RID="${1:-linux-x64}"
case "$RID" in
  linux-x64|linux-arm64) ;;
  *) echo "Error: unsupported RID '$RID'. Use linux-x64 or linux-arm64." >&2; exit 1 ;;
esac

VERSION=$(grep '<Version>' Directory.Build.props 2>/dev/null | sed 's/.*<Version>\(.*\)<\/Version>.*/\1/' | tr -d '[:space:]')
if [ -z "$VERSION" ]; then
  echo "Error: no <Version> element found in Directory.Build.props" >&2
  exit 1
fi

PUBLISH_DIR="DatPlotX/bin/Release/net10.0/$RID/publish"
EXE_PATH="$PUBLISH_DIR/DatPlotX"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="$REPO_ROOT/releases"
STAGE_NAME="DatPlotX-v${VERSION}-${RID}"
TAR_NAME="${STAGE_NAME}.tar.gz"

if [ ! -f "$EXE_PATH" ]; then
  echo "Error: $EXE_PATH not found. Run ./scripts/build-linux.sh $RID first." >&2
  exit 1
fi

mkdir -p "$OUT_DIR"
rm -f "$OUT_DIR/$TAR_NAME"

# Stage into a versioned directory so the tarball extracts to a named folder
# rather than spraying files into the user's cwd.
STAGE_DIR="$(mktemp -d)/$STAGE_NAME"
mkdir -p "$STAGE_DIR"

echo "==> Staging payload..."
# The single-file exe plus the native libraries that must sit beside it
# (SkiaSharp, HarfBuzz, Avalonia's native GL shim).
cp "$EXE_PATH" "$STAGE_DIR/"
find "$PUBLISH_DIR" -maxdepth 1 -name '*.so' -exec cp {} "$STAGE_DIR/" \;
cp "$REPO_ROOT/LICENSE" "$STAGE_DIR/"
chmod +x "$STAGE_DIR/DatPlotX"

echo "==> Creating releases/$TAR_NAME ..."
tar -czf "$OUT_DIR/$TAR_NAME" -C "$(dirname "$STAGE_DIR")" "$STAGE_NAME"
rm -rf "$(dirname "$STAGE_DIR")"

echo ""
echo "Done. Release tarball: $OUT_DIR/$TAR_NAME"
ls -lh "$OUT_DIR/$TAR_NAME"
echo ""
echo "Users install with:"
echo "  tar -xzf $TAR_NAME && ./$STAGE_NAME/DatPlotX"
