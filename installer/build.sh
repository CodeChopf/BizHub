#!/usr/bin/env bash
# BizHub Build Script (Linux / CI)
#
# Publishes both .NET projects for win-x64 (cross-compilation).
# Inno Setup compilation requires Windows or Wine — see options below.
#
# Usage:
#   ./installer/build.sh [--version 1.0.0] [--skip-publish] [--wine]
#
# Requirements:
#   - .NET 8 SDK (https://dotnet.microsoft.com/download)
#   - Wine + Inno Setup 6.1+ (optional, for --wine mode)

set -euo pipefail

# ── Argument parsing ──────────────────────────────────────────────────────────

VERSION="1.0.0"
SKIP_PUBLISH=false
USE_WINE=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version)      VERSION="$2"; shift 2 ;;
        --skip-publish) SKIP_PUBLISH=true; shift ;;
        --wine)         USE_WINE=true; shift ;;
        *)
            echo "Unknown argument: $1" >&2
            echo "Usage: $0 [--version X.Y.Z] [--skip-publish] [--wine]" >&2
            exit 1
            ;;
    esac
done

# ── Paths ─────────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

LAUNCHER_PROJ="$REPO_ROOT/Launcher/BizHubLauncher/BizHubLauncher.csproj"
API_PROJ="$REPO_ROOT/AuraPrints.Api/AuraPrintsApi.csproj"
ISS_FILE="$SCRIPT_DIR/BizHub.iss"

# Output dirs — must match the #define paths in BizHub.iss
LAUNCHER_OUT="$REPO_ROOT/publish/launcher"
API_OUT="$REPO_ROOT/publish/api"

# ── Helpers ───────────────────────────────────────────────────────────────────

step() { echo ""; echo "==> $1"; }

# ── Publish ───────────────────────────────────────────────────────────────────

if [[ "$SKIP_PUBLISH" == "false" ]]; then
    if ! command -v dotnet &>/dev/null; then
        echo "Error: 'dotnet' not found. Install .NET 8 SDK: https://dotnet.microsoft.com/download" >&2
        exit 1
    fi

    step "Publishing BizHubLauncher -> $LAUNCHER_OUT"
    dotnet publish "$LAUNCHER_PROJ" \
        --configuration Release \
        --runtime win-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        --output "$LAUNCHER_OUT" \
        /nologo

    [[ -f "$LAUNCHER_OUT/BizHubLauncher.exe" ]] || { echo "Error: BizHubLauncher.exe not found" >&2; exit 1; }
    echo "  OK: $LAUNCHER_OUT/BizHubLauncher.exe"

    step "Publishing AuraPrintsApi -> $API_OUT"
    dotnet publish "$API_PROJ" \
        --configuration Release \
        --runtime win-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        --output "$API_OUT" \
        /nologo

    [[ -f "$API_OUT/AuraPrintsApi.exe" ]] || { echo "Error: AuraPrintsApi.exe not found" >&2; exit 1; }
    echo "  OK: $API_OUT/AuraPrintsApi.exe"
fi

# ── Inno Setup compilation ────────────────────────────────────────────────────
#
# Inno Setup is a Windows-only tool. Options for Linux/CI:
#
#   Option A — GitHub Actions (recommended):
#     jobs:
#       build-installer:
#         runs-on: windows-latest
#         steps:
#           - uses: actions/checkout@v4
#           - uses: Minionguyjpro/Inno-Setup-Action@v1.2.2
#             with:
#               path: installer/BizHub.iss
#               options: /DMyAppVersion=${{ github.ref_name }}
#
#   Option B — Wine (pass --wine to this script):
#     Install Wine, then install Inno Setup 6 inside Wine:
#       wine InnoSetup-6.x.x.exe /SILENT /SUPPRESSMSGBOXES
#
#   Option C — Windows machine:
#     Transfer the publish/ output and run installer\build.ps1

if [[ "$USE_WINE" == "true" ]]; then
    step "Compiling installer via Wine"

    if ! command -v wine &>/dev/null; then
        echo "Error: 'wine' not found in PATH." >&2; exit 1
    fi

    WINE_ISCC=""
    for candidate in \
        "$HOME/.wine/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe" \
        "$HOME/.wine/drive_c/Program Files/Inno Setup 6/ISCC.exe"
    do
        [[ -f "$candidate" ]] && { WINE_ISCC="$candidate"; break; }
    done

    if [[ -z "$WINE_ISCC" ]]; then
        echo "Error: Inno Setup not found under Wine." >&2
        echo "Install it with: wine InnoSetup-6.x.x.exe /SILENT /SUPPRESSMSGBOXES" >&2
        echo "Download from: https://jrsoftware.org/isinfo.php" >&2
        exit 1
    fi

    mkdir -p "$SCRIPT_DIR/output"
    wine "$WINE_ISCC" "/DMyAppVersion=$VERSION" "$(winepath -w "$ISS_FILE")"

    echo ""
    echo "Installer: $SCRIPT_DIR/output/BizHub-Setup-$VERSION.exe"
else
    echo ""
    echo "NOTE: Inno Setup compilation skipped on Linux."
    echo "      Options to compile the installer:"
    echo "        A) GitHub Actions windows-latest runner (see comment in this script)"
    echo "        B) Re-run with --wine after installing Inno Setup under Wine"
    echo "        C) Run installer\\build.ps1 on a Windows machine"
fi

echo ""
echo "Done."
