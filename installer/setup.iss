; ----------------------------------------------------------------
;  MDFileViewer installer script (Inno Setup 6) - .claude/02-specs.md section 15.1
;  - Input : folder output of the win-x64-folder publish profile (publish-folder)
;  - Output: ReleaseSetup\MDFileViewer-Setup-<version>.exe
;  - Build : run publish-setup.bat at the repository root
;            (directly: ISCC.exe /DMyAppVersion=x.y.z installer\setup.iss)
;  - Version: edit <Version> in Directory.Build.props only. publish-setup.bat reads it
;            and passes /DMyAppVersion=, so the version is never written here.
;  - Keep this file ASCII only: Inno Setup reads a file without a BOM as ANSI.
;    The Korean wizard text comes from Korean.isl.
; ----------------------------------------------------------------

#define MyAppName "MDFileViewer"
; Compiling without /DMyAppVersion shows 0.0-dev, so the mistake is visible at once
#ifndef MyAppVersion
  #define MyAppVersion "0.0-dev"
#endif
#define MyAppExeName "MDFileViewer.exe"
#define MyPublishDir "..\src\MDFileViewer\bin\Release\net10.0-windows\win-x64\publish-folder"

[Setup]
; AppId identifies the app for upgrades and uninstall - never change it
AppId={{F3FFCBCE-5B03-4F83-BAC5-8AEFB5353A22}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppName}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-user install by default (no UAC prompt, %LOCALAPPDATA%\Programs).
; The privileges dialog or /ALLUSERS switches to an all-users install.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\ReleaseSetup
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
; The published app is win-x64 self-contained
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

; User data under %LOCALAPPDATA%\MDFileViewer (logs, WebView2 data) is left on uninstall on purpose.
; Removing the Explorer menu entry (--unregister) on uninstall is added in Phase 9.
