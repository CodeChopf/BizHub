# BizHub Build & Installer Script (Windows)
#
# Publishes both .NET projects and compiles the Inno Setup installer.
#
# Requirements:
#   - .NET 8 SDK (https://dotnet.microsoft.com/download)
#   - Inno Setup 6.1+ (https://jrsoftware.org/isinfo.php)
#
# Usage:
#   .\installer\build.ps1
#   .\installer\build.ps1 -Version 1.2.0
#   .\installer\build.ps1 -SkipPublish    # recompile installer only
#   .\installer\build.ps1 -SkipInnoSetup  # build binaries only

[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [switch]$SkipPublish,
    [switch]$SkipInnoSetup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Paths ──────────────────────────────────────────────────────────────────────

$RepoRoot      = Resolve-Path (Join-Path $PSScriptRoot "..")
$LauncherProj  = Join-Path $RepoRoot "Launcher\BizHubLauncher\BizHubLauncher.csproj"
$ApiProj       = Join-Path $RepoRoot "AuraPrints.Api\AuraPrintsApi.csproj"
$IssFile       = Join-Path $PSScriptRoot "BizHub.iss"

# Output dirs — must match the #define paths in BizHub.iss
$LauncherOut   = Join-Path $RepoRoot "publish\launcher"
$ApiOut        = Join-Path $RepoRoot "publish\api"
$InstallerOut  = Join-Path $PSScriptRoot "output"

# ── Helper ─────────────────────────────────────────────────────────────────────

function Step([string]$Name) {
    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
}

function Assert-FileExists([string]$Path, [string]$Description) {
    if (-not (Test-Path $Path)) {
        Write-Error "$Description not found: $Path"
        exit 1
    }
}

# ── Publish ────────────────────────────────────────────────────────────────────

if (-not $SkipPublish) {
    Step "Publishing BizHubLauncher"
    dotnet publish $LauncherProj `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        --output $LauncherOut `
        /nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Assert-FileExists (Join-Path $LauncherOut "BizHubLauncher.exe") "BizHubLauncher.exe"
    Write-Host "  OK: $LauncherOut\BizHubLauncher.exe" -ForegroundColor Green

    Step "Publishing AuraPrintsApi"
    dotnet publish $ApiProj `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        --output $ApiOut `
        /nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Assert-FileExists (Join-Path $ApiOut "AuraPrintsApi.exe") "AuraPrintsApi.exe"
    Write-Host "  OK: $ApiOut\AuraPrintsApi.exe" -ForegroundColor Green
}

# ── Compile Installer ──────────────────────────────────────────────────────────

if (-not $SkipInnoSetup) {
    # Locate iscc.exe: check PATH first, then default install locations
    $IsccPath = (Get-Command "iscc.exe" -ErrorAction SilentlyContinue)?.Source

    if (-not $IsccPath) {
        $Candidates = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\iscc.exe",
            "${env:ProgramFiles}\Inno Setup 6\iscc.exe"
        )
        $IsccPath = $Candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }

    if (-not $IsccPath) {
        Write-Error "iscc.exe not found. Install Inno Setup 6 from https://jrsoftware.org/isinfo.php"
        exit 1
    }

    Write-Host ""
    Write-Host "  iscc.exe: $IsccPath" -ForegroundColor DarkGray

    New-Item -ItemType Directory -Path $InstallerOut -Force | Out-Null

    Step "Compiling Inno Setup installer (v$Version)"
    & $IsccPath "/DMyAppVersion=$Version" $IssFile
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $InstallerFile = Join-Path $InstallerOut "BizHub-Setup-$Version.exe"
    if (Test-Path $InstallerFile) {
        $SizeMB = [math]::Round((Get-Item $InstallerFile).Length / 1MB, 1)
        Write-Host ""
        Write-Host "Installer ready: $InstallerFile ($SizeMB MB)" -ForegroundColor Green
    } else {
        Write-Warning "Installer file not found at $InstallerFile — check iscc output above."
    }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
