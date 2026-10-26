#ifndef MyAppVersion
  #error "Pass /DMyAppVersion=<version> using scripts/build-installer.ps1."
#endif

#define MyAppName "ClipRack"
#define MyAppPublisher "fukuyori"
#define MyAppExeName "ClipRack.exe"

[Setup]
AppId={{F63AA47C-CE59-47C5-920D-80D9D991C86B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=installer_output
OutputBaseFilename=ClipRack_Setup_{#MyAppVersion}
SetupIconFile=Resources\cliprack.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
#ifdef SIGN
SignTool=clippysign
SignedUninstaller=yes
#endif

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
japanese.CreateDesktopIcon=デスクトップにアイコンを作成する
english.CreateDesktopIcon=Create a desktop icon
japanese.AdditionalIcons=追加アイコン:
english.AdditionalIcons=Additional icons:
japanese.StartWithWindows=Windowsへのサインイン時にClipRackを自動起動する
english.StartWithWindows=Start ClipRack when I sign in to Windows
japanese.AdditionalSettings=追加設定:
english.AdditionalSettings=Additional settings:
japanese.UninstallClipRack=ClipRack をアンインストール
english.UninstallClipRack=Uninstall ClipRack
japanese.LaunchClipRack=ClipRackを起動
english.LaunchClipRack=Launch ClipRack

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "{cm:StartWithWindows}"; GroupDescription: "{cm:AdditionalSettings}"; Flags: unchecked

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ClipRack"; ValueData: """{app}\ClipRack.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Files]
Source: "artifacts\publish\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "artifacts\publish\win-x64\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "artifacts\publish\win-x64\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallClipRack}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchClipRack}"; Flags: nowait postinstall skipifsilent
