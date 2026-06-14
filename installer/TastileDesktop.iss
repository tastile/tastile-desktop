[Setup]
AppId={{F2F2B6E3-5D2B-4B66-8C4B-A4A8A65C54E9}}
AppName=Tastile
AppVersion={#AppVersion}
AppPublisher=Tastile
AppVerName=Tastile
DefaultDirName={autopf}\Tastile
DefaultGroupName=Tastile
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=tastile-desktop-{#AppVersion}-setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile={#SourceDir}\Assets\tastile.ico
UninstallDisplayIcon={app}\TastileDesktop.exe
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"
Name: "startup"; Description: "Start Tastile when Windows starts"; GroupDescription: "Startup:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Tastile"; Filename: "{app}\TastileDesktop.exe"
Name: "{autodesktop}\Tastile"; Filename: "{app}\TastileDesktop.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TastileDesktop"; ValueData: """{app}\TastileDesktop.exe"" --minimized"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\TastileDesktop.exe"; Description: "Launch Tastile"; Flags: nowait postinstall skipifsilent
Filename: "{app}\TastileDesktop.exe"; Parameters: "--minimized"; Flags: nowait skipifnotsilent
