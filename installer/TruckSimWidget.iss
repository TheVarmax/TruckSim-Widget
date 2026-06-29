#define MyAppName "TruckSim Widget"
#define MyAppVersion "1.5.0-beta"
#define MyAppExeName "TruckSim Widget.exe"
#define PublishDir "C:\Users\mrpry\Desktop\TruckSim Widget\TruckSim Widget (1.5.0-beta)"

[Setup]
AppId={{8F4E6E2C-7F11-4F7D-BD7D-TRUCKSIM155BETA}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=TheVarmax
AppPublisherURL=https://trucksim.maksym.uk
AppSupportURL=https://trucksim.maksym.uk
AppUpdatesURL=https://github.com/TheVarmax/TruckSim-Widget/releases
DefaultDirName={localappdata}\Programs\TruckSim Widget
DefaultGroupName=TruckSim Widget
DisableProgramGroupPage=yes
OutputDir=C:\Users\mrpry\Desktop\TruckSim Widget\Releases
OutputBaseFilename=TruckSimWidgetSetup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
SetupIconFile=..\favicon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
LZMANumBlockThreads=8

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TruckSim Widget"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\TruckSim Widget"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch TruckSim Widget"; Flags: nowait postinstall skipifsilent