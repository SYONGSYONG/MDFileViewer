; ----------------------------------------------------------------
;  MDFileViewer installer script (Inno Setup 6) - .claude/02-specs.md section 15.1
;  - Input : folder output of the win-x64-folder publish profile (publish-folder)
;  - Output: ReleaseSetup\MDFileViewer-Setup-<version>.exe
;  - Build : run publish-setup.bat at the repository root
;            (directly: ISCC.exe /DMyAppVersion=x.y.z installer\setup.iss)
;  - Version: edit <Version> in Directory.Build.props only. publish-setup.bat reads it
;            and passes /DMyAppVersion=, so the version is never written here.
;  - Keep this file ASCII only: Inno Setup reads a file without a BOM as ANSI.
;    The Korean wizard text comes from Korean.isl; the text this script adds is in
;    installer\Korean.custom.isl (UTF-8 with BOM), referenced below as {cm:...}.
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
AppPublisher=SYONG
AppCopyright=Copyright (c) 2026 SYONG
; MIT license page in the wizard; LICENSE.txt and THIRD-PARTY-NOTICES.txt are also installed from the publish folder (.claude/02-specs.md section 15.2)
LicenseFile=..\LICENSE
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
; Setup and Uninstall ask the user to close the app while this mutex exists. Without it, uninstalling
; with the app open leaves locked files behind ("some elements could not be removed").
; Must equal AppConstants.InstanceMutexName (checked by InstallerScriptTests).
AppMutex=MDFileViewer.Instance.F3FFCBCE-5B03-4F83-BAC5-8AEFB5353A22
SetupIconFile=..\src\MDFileViewer\Assets\app.ico

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl,Korean.custom.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
; Checked by default (no "unchecked" flag). An update install starts from the previous choice (UsePreviousTasks).
; Unchecking it does not remove an existing entry - the app menu does that.
Name: "shellmenu"; Description: "{cm:ShellMenuTask}"; GroupDescription: "{cm:ShellMenuGroup}"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Add the Explorer context menu entry (HKCU). The app owns the registry layout, so Setup asks the app to do it.
; runasoriginaluser: in an elevated all-users install the entry must still go to the HKCU of the person installing.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--register --silent"; Flags: runhidden waituntilterminated runasoriginaluser; Tasks: shellmenu
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove the Explorer context menu entry (HKCU) before the files go. --silent suppresses the result box.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--unregister --silent"; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterShellMenu"

; User data under %LOCALAPPDATA%\MDFileViewer and %APPDATA%\MDFileViewer (logs, WebView2 data, settings) is left on uninstall on purpose.
