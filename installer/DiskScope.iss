#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "output"
#endif

[Setup]
AppId={{9C60D121-A5BD-4C57-BD44-34119F8D90D4}
AppName=DiskScope
AppVersion={#MyAppVersion}
AppPublisher=DiskScope contributors
AppPublisherURL=https://github.com/pcalsys/DiskScope
AppSupportURL=https://github.com/pcalsys/DiskScope/issues
AppUpdatesURL=https://github.com/pcalsys/DiskScope/releases
DefaultDirName={localappdata}\Programs\DiskScope
DefaultGroupName=DiskScope
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=DiskScope-{#MyAppVersion}-win-x64-setup
SetupIconFile=..\src\DiskScope\Assets\DiskScope.ico
UninstallDisplayIcon={app}\DiskScope.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany=DiskScope contributors
VersionInfoDescription=DiskScope installer
VersionInfoProductName=DiskScope
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\DiskScope"; Filename: "{app}\DiskScope.exe"
Name: "{autodesktop}\DiskScope"; Filename: "{app}\DiskScope.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DiskScope.exe"; Description: "{cm:LaunchProgram,DiskScope}"; Flags: nowait postinstall skipifsilent
