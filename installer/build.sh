#!/usr/bin/env bash
# BizHub Build Script (Linux / CI)
# Publishes both .NET projects for win-x64.
# Note: Inno Setup compilation requires Windows or Wine.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$REPO_ROOT/build"
VERSION="${1:-1.0.0}"

echo "=== BizHub Build (Linux/CI) ==="
echo "Version:   $VERSION"
echo "Repo Root: $REPO_ROOT"
echo "Build Dir: $BUILD_DIR"
echo ""

# Clean and recreate build directory
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# Publish AuraPrints API
echo "Publishing AuraPrintsApi..."
dotnet publish "$REPO_ROOT/AuraPrints.Api/AuraPrintsApi.csproj" \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -o "$BUILD_DIR"

# Publish BizHub Launcher
echo "Publishing BizHubLauncher..."
dotnet publish "$REPO_ROOT/Launcher/BizHubLauncher/BizHubLauncher.csproj" \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -o "$BUILD_DIR"

echo ""
echo "Build artifacts in $BUILD_DIR:"
ls -lh "$BUILD_DIR"/*.exe 2>/dev/null || echo "  (no .exe files found)"

# WebView2 bootstrapper check
WEBVIEW2_BOOTSTRAPPER="$SCRIPT_DIR/MicrosoftEdgeWebview2Setup.exe"
if [ ! -f "$WEBVIEW2_BOOTSTRAPPER" ]; then
    echo ""
    echo "WARNING: MicrosoftEdgeWebview2Setup.exe not found in installer/"
    echo "Download it from: https://go.microsoft.com/fwlink/p/?LinkId=2124703"
    echo "Place it in $SCRIPT_DIR/ before compiling the installer."
fi

# Inno Setup compilation
echo ""
echo "=== Inno Setup Compilation ==="

# Try Wine-based Inno Setup first (common in CI)
ISCC_WINE_PATH="${ISCC_PATH:-/opt/wine-stable/bin/wine}"
ISCC_EXE="${INNO_SETUP_ISCC:-$HOME/.wine/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe}"

if command -v wine &>/dev/null && [ -f "$ISCC_EXE" ]; then
    echo "Compiling installer via Wine..."
    wine "$ISCC_EXE" "/DAppVersion=$VERSION" "$(winepath -w "$SCRIPT_DIR/BizHub.iss")"
    echo ""
    echo "=== SUCCESS ==="
    echo "Installer: $SCRIPT_DIR/Output/BizHub_Setup_$VERSION.exe"
else
    echo "Inno Setup (ISCC.exe) not found or Wine not available."
    echo ""
    echo "To compile the installer, use one of the following options:"
    echo ""
    echo "  Option 1: Windows"
    echo "    Run installer/build.ps1 on a Windows machine"
    echo ""
    echo "  Option 2: Wine on Linux"
    echo "    1. Install Wine and Inno Setup 6"
    echo "    2. Set INNO_SETUP_ISCC to the path of ISCC.exe"
    echo "    3. Re-run this script"
    echo ""
    echo "  Option 3: GitHub Actions"
    echo "    Use a windows-latest runner in your CI workflow"
    echo "    (see .github/workflows/ for examples)"
    echo ""
    echo ".NET build artifacts are ready in: $BUILD_DIR"
fi
