[Setup]
AppId={{F2F2B6E3-5D2B-4B66-8C4B-A4A8A65C54E9}
AppName=Tastile Desktop
AppVersion={#AppVersion}
AppPublisher=Tastile
DefaultDirName={autopf}\Tastile Desktop
DefaultGroupName=Tastile Desktop
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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"
Name: "startup"; Description: "Start Tastile Desktop when Windows starts"; GroupDescription: "Startup:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Tastile Desktop"; Filename: "{app}\TastileDesktop.exe"
Name: "{autodesktop}\Tastile Desktop"; Filename: "{app}\TastileDesktop.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TastileDesktop"; ValueData: """{app}\TastileDesktop.exe"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\TastileDesktop.exe"; Description: "Launch Tastile Desktop"; Flags: nowait postinstall skipifsilent
