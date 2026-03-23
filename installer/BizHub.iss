; BizHub Inno Setup Installer Script
; Requires Inno Setup 6.x: https://jrsoftware.org/isinfo.php

#define AppName "BizHub"
#define AppVersion "1.0.0"
#define AppPublisher "BizHub"
#define AppExeName "BizHubLauncher.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/CodeChopf/BizHub
AppSupportURL=https://github.com/CodeChopf/BizHub/issues
AppUpdatesURL=https://github.com/CodeChopf/BizHub/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=BizHub_Setup_{#AppVersion}
SetupIconFile=..\Launcher\BizHubLauncher\favicon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
CloseApplications=yes

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main application executables (both must be in the same directory)
Source: "..\build\BizHubLauncher.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\AuraPrintsApi.exe"; DestDir: "{app}"; Flags: ignoreversion

; WebView2 bootstrapper - only included if WebView2 is not already installed
; Download MicrosoftEdgeWebview2Setup.exe from:
; https://go.microsoft.com/fwlink/p/?LinkId=2124703
; and place it in the installer/ directory before building
Source: "MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsWebView2

[Icons]
; Start Menu entry
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"

; Optional Desktop shortcut (only if task selected)
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Offer to launch BizHub after installation
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove the data directory created by the app on uninstall (optional, user data)
; Type: filesandordirs; Name: "{app}\Data"

[Code]

// Check if WebView2 runtime is installed
function NeedsWebView2: Boolean;
var
  RegKey: String;
  RegValue: String;
begin
  RegKey := 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  // Check machine-wide installation
  if RegQueryStringValue(HKLM, RegKey, 'pv', RegValue) then
  begin
    if (RegValue <> '') and (RegValue <> '0.0.0.0') then
    begin
      Result := False;
      Exit;
    end;
  end;

  // Check user-level installation
  RegKey := 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  if RegQueryStringValue(HKCU, RegKey, 'pv', RegValue) then
  begin
    if (RegValue <> '') and (RegValue <> '0.0.0.0') then
    begin
      Result := False;
      Exit;
    end;
  end;

  Result := True;
end;

// Install WebView2 if needed before copying app files
procedure CurStepChanged(CurStep: TSetupStep);
var
  WebView2Installer: String;
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    if NeedsWebView2 then
    begin
      WebView2Installer := ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe');
      if FileExists(WebView2Installer) then
      begin
        if not Exec(WebView2Installer, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
        begin
          MsgBox('WebView2 konnte nicht installiert werden (Fehlercode: ' + IntToStr(ResultCode) + ').'
            + #13#10 + 'BizHub benoetigt WebView2. Bitte installieren Sie es manuell von:'
            + #13#10 + 'https://go.microsoft.com/fwlink/p/?LinkId=2124703',
            mbError, MB_OK);
        end;
      end;
    end;
  end;
end;

// Show a warning if WebView2 bootstrapper is missing from the installer package
function InitializeSetup(): Boolean;
begin
  Result := True;
  if NeedsWebView2 then
  begin
    if not FileExists(ExpandConstant('{src}\MicrosoftEdgeWebview2Setup.exe')) then
    begin
      MsgBox('Hinweis: WebView2 Runtime ist nicht installiert und der Bootstrapper fehlt im Installer-Paket.'
        + #13#10 + 'BizHub benoetigt WebView2. Bitte installieren Sie es nach der Installation manuell von:'
        + #13#10 + 'https://go.microsoft.com/fwlink/p/?LinkId=2124703',
        mbInformation, MB_OK);
    end;
  end;
end;
