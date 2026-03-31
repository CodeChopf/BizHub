; BizHub Inno Setup Installer Script
; Requires Inno Setup 6.1+: https://jrsoftware.org/isinfo.php
;
; Build with:
;   Windows: installer\build.ps1
;   Linux/CI: installer\build.sh

#define MyAppName     "BizHub"
#define MyAppVersion  "1.0.0"
#define MyAppPublisher "BizHub"
#define MyAppExeName  "BizHubLauncher.exe"
#define MyApiExeName  "AuraPrintsApi.exe"

; Published binary paths (relative to this .iss file)
; build.ps1 / build.sh publish into publish/launcher/ and publish/api/
#define LauncherSrc "..\publish\launcher\BizHubLauncher.exe"
#define ApiSrc      "..\publish\api\AuraPrintsApi.exe"
#define IconSrc     "..\Launcher\BizHubLauncher\favicon.ico"

[Setup]
; NOTE: Keep AppId stable across versions for correct upgrade detection
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/CodeChopf/BizHub
AppSupportURL=https://github.com/CodeChopf/BizHub/issues
AppUpdatesURL=https://github.com/CodeChopf/BizHub/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=BizHub-Setup-{#MyAppVersion}
SetupIconFile={#IconSrc}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; 64-bit Windows only (matches win-x64 build target)
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Minimum Windows 10 1809 (required for WebView2)
MinVersion=10.0.17763
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
CloseApplications=yes

[Languages]
Name: "german";  MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Both executables must be in the same directory — the launcher finds
; AuraPrintsApi.exe via AppDomain.CurrentDomain.BaseDirectory
Source: "{#LauncherSrc}"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion
Source: "{#ApiSrc}";      DestDir: "{app}"; DestName: "{#MyApiExeName}"; Flags: ignoreversion

; Frontend static files (HTML/CSS/JS) served by the API
Source: "..\publish\api\wwwroot\*"; DestDir: "{app}\wwwroot"; Flags: ignoreversion recursesubdirs createallsubdirs

; App icon for shortcuts
Source: "{#IconSrc}"; DestDir: "{app}"; DestName: "favicon.ico"; Flags: ignoreversion

[Dirs]
; AuraPrintsApi.exe creates Data\auraprints.db here at runtime.
; uninsneveruninstall preserves user data when the app is uninstalled.
Name: "{app}\Data"; Flags: uninsneveruninstall

[Icons]
; Start Menu entry
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\favicon.ico"; Comment: "BizHub starten"
Name: "{autoprograms}\{#MyAppName}\{#MyAppName} deinstallieren"; Filename: "{uninstallexe}"

[Run]
; Offer to launch BizHub after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]

const
  // WebView2 stable channel GUID (do not change)
  WebView2Guid = '{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  // Microsoft's official evergreen bootstrapper URL
  WebView2BootstrapperURL = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703';

// Returns True when WebView2 is NOT installed (i.e. installation is needed)
function NeedsWebView2: Boolean;
var
  Version: String;
begin
  // Machine-wide install (enterprise MSI / system-level bootstrapper)
  if RegQueryStringValue(HKLM,
      'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\' + WebView2Guid,
      'pv', Version) then
  begin
    if (Version <> '') and (Version <> '0.0.0.0') then
    begin
      Result := False;
      Exit;
    end;
  end;

  // Per-user install (evergreen bootstrapper default)
  if RegQueryStringValue(HKCU,
      'Software\Microsoft\EdgeUpdate\Clients\' + WebView2Guid,
      'pv', Version) then
  begin
    if (Version <> '') and (Version <> '0.0.0.0') then
    begin
      Result := False;
      Exit;
    end;
  end;

  Result := True;
end;

// Called before the wizard is shown — downloads and installs WebView2 if needed
function InitializeSetup(): Boolean;
var
  BootstrapperPath: String;
  ResultCode: Integer;
begin
  Result := True;

  if not NeedsWebView2 then
  begin
    Log('WebView2 is already installed.');
    Exit;
  end;

  // Inform the user and ask for consent before downloading
  if MsgBox(
    'BizHub benötigt die Microsoft WebView2 Runtime, die aktuell nicht installiert ist.' + #13#10#13#10 +
    'Der Installer lädt jetzt den WebView2-Bootstrapper von Microsoft herunter' + #13#10 +
    'und installiert ihn automatisch. Eine Internetverbindung ist erforderlich.' + #13#10#13#10 +
    'Auf OK klicken, um fortzufahren, oder Abbrechen zum Beenden.',
    mbConfirmation, MB_OKCANCEL) = IDCANCEL then
  begin
    Result := False;
    Exit;
  end;

  // DownloadTemporaryFile is built into Inno Setup 6.1+ (no plugin required)
  BootstrapperPath := ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe');

  Log('Downloading WebView2 bootstrapper...');
  try
    DownloadTemporaryFile(WebView2BootstrapperURL, 'MicrosoftEdgeWebview2Setup.exe', '', nil);
  except
    MsgBox(
      'Der Download des WebView2-Bootstrappers ist fehlgeschlagen.' + #13#10 +
      'Bitte installieren Sie WebView2 manuell von:' + #13#10 +
      'https://developer.microsoft.com/en-us/microsoft-edge/webview2/' + #13#10#13#10 +
      'Die Installation wird fortgesetzt, aber BizHub startet möglicherweise' + #13#10 +
      'erst nach der manuellen WebView2-Installation.',
      mbError, MB_OK);
    Exit; // Continue installation without WebView2
  end;

  if not FileExists(BootstrapperPath) then
  begin
    MsgBox(
      'Die heruntergeladene WebView2-Datei wurde nicht gefunden.' + #13#10 +
      'Bitte installieren Sie WebView2 manuell nach der Installation.',
      mbError, MB_OK);
    Exit;
  end;

  // Run bootstrapper silently
  Log('Running WebView2 bootstrapper...');
  if not Exec(BootstrapperPath, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox(
      'Der WebView2-Bootstrapper konnte nicht gestartet werden (Fehlercode: ' + IntToStr(ResultCode) + ').' + #13#10 +
      'Bitte installieren Sie WebView2 manuell und starten Sie BizHub erneut.',
      mbError, MB_OK);
    Result := False;
    Exit;
  end;

  Log('WebView2 bootstrapper completed with exit code: ' + IntToStr(ResultCode));

  if NeedsWebView2 then
    MsgBox(
      'Die WebView2-Installation war möglicherweise nicht erfolgreich.' + #13#10 +
      'Falls BizHub nicht startet, installieren Sie WebView2 bitte manuell von:' + #13#10 +
      'https://developer.microsoft.com/en-us/microsoft-edge/webview2/',
      mbInformation, MB_OK);
end;
