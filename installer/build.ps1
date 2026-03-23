# BizHub Build & Installer Script
# Run this script on Windows to build the app and create the installer.
# Requirements:
#   - .NET 8 SDK (https://dotnet.microsoft.com/download)
#   - Inno Setup 6 (https://jrsoftware.org/isinfo.php)
#   - MicrosoftEdgeWebview2Setup.exe in the installer/ directory
#     (download from https://go.microsoft.com/fwlink/p/?LinkId=2124703)

param(
    [string]$Version = "1.0.0",
    [string]$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$RepoRoot = Resolve-Path "$ScriptDir\.."
$BuildDir = "$RepoRoot\build"
$OutputDir = "$ScriptDir\Output"

Write-Host "=== BizHub Build ===" -ForegroundColor Cyan
Write-Host "Version:    $Version"
Write-Host "Repo Root:  $RepoRoot"
Write-Host "Build Dir:  $BuildDir"
Write-Host ""

# Clean build directory
if (Test-Path $BuildDir) {
    Write-Host "Cleaning build directory..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $BuildDir
}
New-Item -ItemType Directory -Path $BuildDir | Out-Null

# Publish AuraPrints API
Write-Host "Publishing AuraPrintsApi..." -ForegroundColor Green
$ApiProject = "$RepoRoot\AuraPrints.Api\AuraPrintsApi.csproj"
dotnet publish $ApiProject -c Release -r win-x64 --self-contained true -o $BuildDir
if ($LASTEXITCODE -ne 0) { throw "Failed to publish AuraPrintsApi" }

# Publish BizHub Launcher
Write-Host "Publishing BizHubLauncher..." -ForegroundColor Green
$LauncherProject = "$RepoRoot\Launcher\BizHubLauncher\BizHubLauncher.csproj"
dotnet publish $LauncherProject -c Release -r win-x64 --self-contained true -o $BuildDir
if ($LASTEXITCODE -ne 0) { throw "Failed to publish BizHubLauncher" }

Write-Host ""
Write-Host "Build artifacts:" -ForegroundColor Cyan
Get-ChildItem $BuildDir -Filter "*.exe" | ForEach-Object { Write-Host "  $_" }

# Check for WebView2 bootstrapper
$WebView2Bootstrapper = "$ScriptDir\MicrosoftEdgeWebview2Setup.exe"
if (-not (Test-Path $WebView2Bootstrapper)) {
    Write-Host ""
    Write-Host "WARNING: MicrosoftEdgeWebview2Setup.exe not found in installer/" -ForegroundColor Yellow
    Write-Host "The installer will be built without bundled WebView2 bootstrapper." -ForegroundColor Yellow
    Write-Host "Download it from: https://go.microsoft.com/fwlink/p/?LinkId=2124703" -ForegroundColor Yellow
}

# Build Inno Setup installer
if (-not (Test-Path $InnoSetupPath)) {
    Write-Host ""
    Write-Host "ERROR: Inno Setup not found at: $InnoSetupPath" -ForegroundColor Red
    Write-Host "Please install Inno Setup 6 from https://jrsoftware.org/isinfo.php" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Compiling installer with Inno Setup..." -ForegroundColor Green
$IssFile = "$ScriptDir\BizHub.iss"
& $InnoSetupPath "/DAppVersion=$Version" $IssFile
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed" }

$InstallerFile = "$OutputDir\BizHub_Setup_$Version.exe"
if (Test-Path $InstallerFile) {
    Write-Host ""
    Write-Host "=== SUCCESS ===" -ForegroundColor Green
    Write-Host "Installer created: $InstallerFile" -ForegroundColor Green
    $Size = (Get-Item $InstallerFile).Length / 1MB
    Write-Host "Installer size: $([math]::Round($Size, 1)) MB" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "ERROR: Installer file not found at expected path: $InstallerFile" -ForegroundColor Red
    exit 1
}
