#define AppName "NovaLauncher"
#define AppVersion "0.5.0-experimental.1"
#define AppPublisher "NovaLauncher Contributors"

[Setup]
AppId={{A348AA9D-C3D4-49B9-82F0-F9A82F34FD11}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} unsigned beta preview installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion=0.3.0.1
DefaultDirName={localappdata}\Programs\NovaLauncher
DefaultGroupName=NovaLauncher
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\release
OutputBaseFilename=NovaLauncher-Setup-0.5.0-experimental.1-win-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern dynamic
WizardSizePercent=110
LicenseFile=..\LICENSE
InfoBeforeFile=UNSIGNED-PREVIEW.txt
UninstallDisplayIcon={app}\NovaLauncher.App.exe
ChangesAssociations=no
ChangesEnvironment=no
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
DisableProgramGroupPage=yes

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "UNSIGNED-PREVIEW.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\NovaLauncher"; Filename: "{app}\NovaLauncher.App.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\NovaLauncher"; Filename: "{app}\NovaLauncher.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\NovaLauncher.App.exe"; Description: "Launch NovaLauncher"; Flags: nowait postinstall skipifsilent
