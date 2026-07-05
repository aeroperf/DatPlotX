#!/bin/bash
# Creates a distributable zip of the macOS .app bundle.
# The zip is written to the repo-root releases/ directory (not inside the
# publish folder) so it cannot be accidentally bundled into future builds.
#
# Run from the repo root: ./scripts/zip-macos-release.sh
set -e

VERSION=$(grep '<Version>' Directory.Build.props 2>/dev/null | sed 's/.*<Version>\(.*\)<\/Version>.*/\1/' | tr -d '[:space:]')
if [ -z "$VERSION" ]; then
  VERSION="1.0.0"
fi

PUBLISH_DIR="DatPlotX/bin/Release/net10.0/osx-arm64/publish"
APP_DIR="$PUBLISH_DIR/DatPlotX.app"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="$REPO_ROOT/releases"
ZIP_NAME="DatPlotX-v${VERSION}-osx-arm64.zip"

if [ ! -d "$APP_DIR" ]; then
  echo "Error: $APP_DIR not found. Run ./scripts/build-macos-app.sh first."
  exit 1
fi

mkdir -p "$OUT_DIR"

echo "==> Creating releases/$ZIP_NAME ..."
cd "$PUBLISH_DIR"
ditto -ck --sequesterRsrc --keepParent "DatPlotX.app" "$OUT_DIR/$ZIP_NAME"
cd - > /dev/null

echo ""
echo "Done. Release zip: $OUT_DIR/$ZIP_NAME"
ls -lh "$OUT_DIR/$ZIP_NAME"
