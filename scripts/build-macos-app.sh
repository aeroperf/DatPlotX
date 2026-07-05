#!/bin/bash
# Builds and assembles the DatPlotX macOS .app bundle.
# Run from the repo root: ./scripts/build-macos-app.sh
set -e

# Read version from Directory.Build.props (falls back to 1.0.0 if not set)
VERSION=$(grep '<Version>' Directory.Build.props 2>/dev/null | sed 's/.*<Version>\(.*\)<\/Version>.*/\1/' | tr -d '[:space:]')
if [ -z "$VERSION" ]; then
  VERSION="1.0.0"
fi
echo "==> Version: $VERSION"

PUBLISH_DIR="DatPlotX/bin/Release/net10.0/osx-arm64/publish"
APP_DIR="$PUBLISH_DIR/DatPlotX.app"

echo "==> Cleaning previous publish output..."
rm -rf "$PUBLISH_DIR"

echo "==> Publishing (self-contained, osx-arm64)..."
dotnet publish DatPlotX/DatPlotX.csproj -c Release -r osx-arm64 --self-contained true

echo "==> Removing previous bundle (if any)..."
rm -rf "$APP_DIR"

echo "==> Creating bundle structure..."
mkdir -p "$APP_DIR/Contents/MacOS"
mkdir -p "$APP_DIR/Contents/Resources"

echo "==> Copying published binaries..."
find "$PUBLISH_DIR" -maxdepth 1 ! -name "*.app" ! -name "*.zip" -not -path "$PUBLISH_DIR" \
  -exec cp -R {} "$APP_DIR/Contents/MacOS/" \;

# Copy .icns icon if present
if [ -f "DatPlotX/Assets/icon.icns" ]; then
  echo "==> Copying icon..."
  cp "DatPlotX/Assets/icon.icns" "$APP_DIR/Contents/Resources/DatPlotX.icns"
  ICON_FILE="DatPlotX"
else
  echo "==> Warning: no icon.icns found in DatPlotX/Assets — bundle will have no icon."
  ICON_FILE=""
fi

echo "==> Writing Info.plist (version $VERSION)..."
cat > "$APP_DIR/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>DatPlotX</string>
    <key>CFBundleDisplayName</key>
    <string>DatPlotX</string>
    <key>CFBundleIdentifier</key>
    <string>com.aeroperf.datplotx</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundleExecutable</key>
    <string>DatPlotX</string>
    <key>CFBundleIconFile</key>
    <string>$ICON_FILE</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>CFBundleDocumentTypes</key>
    <array>
        <dict>
            <key>CFBundleTypeName</key>
            <string>DatPlotX Project</string>
            <key>CFBundleTypeExtensions</key>
            <array>
                <string>dpx</string>
            </array>
            <key>CFBundleTypeRole</key>
            <string>Editor</string>
            <key>CFBundleTypeIconFile</key>
            <string>$ICON_FILE</string>
            <key>LSHandlerRank</key>
            <string>Owner</string>
        </dict>
    </array>
</dict>
</plist>
EOF

echo "==> Setting executable permission..."
chmod +x "$APP_DIR/Contents/MacOS/DatPlotX"

echo ""
echo "Done. Bundle: $APP_DIR"
echo ""
echo "To open:      open \"$APP_DIR\""
echo "To zip:       ./scripts/zip-macos-release.sh"
echo "Gatekeeper:   xattr -cr \"$APP_DIR\""
