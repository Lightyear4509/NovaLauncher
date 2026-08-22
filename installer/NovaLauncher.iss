#define AppName "NovaLauncher"
#define AppVersion "0.7.0-alpha.1"
#define AppPublisher "NovaLauncher Contributors"
#ifndef ArtifactSuffix
  #define ArtifactSuffix ""
#endif
#ifndef UnsignedPreview
  #define UnsignedPreview 0
#endif

[Setup]
AppId={{A348AA9D-C3D4-49B9-82F0-F9A82F34FD11}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} {#if UnsignedPreview}unsigned preview{#else}signed lifecycle candidate{#endif} installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion=0.7.0.1
DefaultDirName={localappdata}\Programs\NovaLauncher
DefaultGroupName=NovaLauncher
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\release
OutputBaseFilename=NovaLauncher-Setup-0.7.0-alpha.1{#ArtifactSuffix}-win-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern dynamic
WizardSizePercent=110
LicenseFile=..\LICENSE
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

[Icons]
Name: "{group}\NovaLauncher"; Filename: "{app}\NovaLauncher.App.exe"; WorkingDir: "{app}"
Name: "{group}\NovaLauncher Update Recovery"; Filename: "{app}\NovaLauncher.App.exe"; Parameters: "--rollback-update"; WorkingDir: "{app}"; Comment: "Reopen the verified previous NovaLauncher installer after a failed update"
Name: "{userdesktop}\NovaLauncher"; Filename: "{app}\NovaLauncher.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\NovaLauncher.App.exe"; Description: "Launch NovaLauncher"; Flags: nowait postinstall skipifsilent

#if UnsignedPreview
[Code]
function InitializeSetup(): Boolean;
begin
  Result := MsgBox(
    'This NovaLauncher preview is not digitally signed. Windows cannot verify its publisher. ' +
    'Install only if you downloaded it from the official GitHub release and verified its SHA-256 checksum.' + #13#10 + #13#10 +
    'Continue with the unsigned preview?',
    mbConfirmation,
    MB_YESNO) = IDYES;
end;
#else
[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  CacheDirectory: String;
  CachedInstaller: String;
begin
  if CurStep = ssPostInstall then
  begin
    CacheDirectory := ExpandConstant('{localappdata}\NovaLauncher\Updates\InstallerCache');
    CachedInstaller := CacheDirectory + '\NovaLauncher-Setup-{#AppVersion}-win-x64.exe';
    if not ForceDirectories(CacheDirectory) then
      RaiseException('NovaLauncher could not create its signed installer recovery cache.');
    if not CopyFile(ExpandConstant('{srcexe}'), CachedInstaller, False) then
      RaiseException('NovaLauncher could not preserve the signed installer for update recovery.');
  end;
end;
#endif
