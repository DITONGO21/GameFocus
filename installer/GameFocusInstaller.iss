; Inno Setup script to create installer for GameFocus
[Setup]
AppName=GameFocus
AppVersion=0.1.0
DefaultDirName={autopf}\GameFocus
DefaultGroupName=GameFocus
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\assets\logo.ico
UninstallDisplayIcon={app}\GameFocus.UI.exe
OutputDir=..\artifacts\installer
OutputBaseFilename=GameFocus-Setup
Compression=lzma2
SolidCompression=yes

[Files]
Source: "..\artifacts\folder\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Use the actual published executable name (GameFocus.UI.exe). If you changed the project
; assembly name adjust this value accordingly.
Name: "{group}\GameFocus"; Filename: "{app}\GameFocus.UI.exe"

[Run]
Filename: "{app}\GameFocus.UI.exe"; Description: "Launch GameFocus"; Flags: nowait postinstall skipifsilent
