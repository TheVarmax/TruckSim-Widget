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
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[CustomMessages]
english.DesktopIcon=Create a desktop shortcut
english.AdditionalShortcuts=Additional shortcuts:
english.LaunchApp=Launch TruckSim Widget
english.OpenPluginFolder=Open telemetry plugin folder
english.OpenPluginFolderAfterInstall=Open telemetry plugin folder after installation
english.TelemetryPageTitle=Telemetry Plugin Setup
english.TelemetryPageSub=Configure ETS2 and ATS telemetry automatically.
english.TelemetryPageDesc=TruckSim Widget needs scs-telemetry.dll inside each game's plugins folder. Select the games you want the installer to configure. You can skip this step and install the plugin manually later.
english.InstallETS2Plugin=Install telemetry plugin for Euro Truck Simulator 2
english.InstallATSPlugin=Install telemetry plugin for American Truck Simulator
english.GameDirPageTitle=Game folders
english.GameDirPageSub=Choose the root folders of your installed games.
english.GameDirPageDesc=Select the game folder that contains bin\win_x64. The installer will create bin\win_x64\plugins if needed and copy scs-telemetry.dll there.
english.ETS2DirPrompt=Euro Truck Simulator 2 folder:
english.ATSDirPrompt=American Truck Simulator folder:
english.PathRequired=Please choose a game folder, or go back and untick this game.
english.PathLooksWrong=This folder does not look like the selected game folder:%n%n%1%n%nExpected file:%n%2%n%nContinue anyway?
english.PluginInstallFailed=Could not install the telemetry plugin for %1.%n%nYou can still copy scs-telemetry.dll manually from:%n%2
english.PluginInstalled=Telemetry plugin installed for %1.
english.PluginInstallSummary=Telemetry plugin setup finished.

ukrainian.DesktopIcon=Створити ярлик на робочому столі
ukrainian.AdditionalShortcuts=Додаткові ярлики:
ukrainian.LaunchApp=Запустити TruckSim Widget
ukrainian.OpenPluginFolder=Відкрити папку плагіна телеметрії
ukrainian.OpenPluginFolderAfterInstall=Відкрити папку плагіна телеметрії після встановлення
ukrainian.TelemetryPageTitle=Налаштування плагіна телеметрії
ukrainian.TelemetryPageSub=Автоматично налаштуй телеметрію для ETS2 та ATS.
ukrainian.TelemetryPageDesc=TruckSim Widget потребує файл scs-telemetry.dll у папці plugins кожної гри. Обери ігри, які інсталятор має налаштувати. Цей крок можна пропустити й встановити плагін вручну пізніше.
ukrainian.InstallETS2Plugin=Встановити плагін телеметрії для Euro Truck Simulator 2
ukrainian.InstallATSPlugin=Встановити плагін телеметрії для American Truck Simulator
ukrainian.GameDirPageTitle=Папки ігор
ukrainian.GameDirPageSub=Обери кореневі папки встановлених ігор.
ukrainian.GameDirPageDesc=Обери папку гри, у якій є bin\win_x64. Інсталятор створить bin\win_x64\plugins, якщо потрібно, і скопіює туди scs-telemetry.dll.
ukrainian.ETS2DirPrompt=Папка Euro Truck Simulator 2:
ukrainian.ATSDirPrompt=Папка American Truck Simulator:
ukrainian.PathRequired=Обери папку гри або повернися назад і зніми позначку з цієї гри.
ukrainian.PathLooksWrong=Ця папка не схожа на папку вибраної гри:%n%n%1%n%nОчікуваний файл:%n%2%n%nПродовжити все одно?
ukrainian.PluginInstallFailed=Не вдалося встановити плагін телеметрії для %1.%n%nТи все ще можеш скопіювати scs-telemetry.dll вручну з:%n%2
ukrainian.PluginInstalled=Плагін телеметрії встановлено для %1.
ukrainian.PluginInstallSummary=Налаштування плагіна телеметрії завершено.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalShortcuts}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TruckSim Widget"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Open telemetry plugin folder"; Filename: "{app}\plugin"
Name: "{autodesktop}\TruckSim Widget"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\plugin"; Description: "{cm:OpenPluginFolderAfterInstall}"; Flags: postinstall shellexec skipifsilent

[Code]
var
  TelemetryPage: TInputOptionWizardPage;
  GameDirPage: TInputDirWizardPage;

function CombinePath(BasePath: String; RelativePath: String): String;
begin
  Result := AddBackslash(BasePath) + RelativePath;
end;

function GetSteamInstallPath(): String;
begin
  Result := '';

  if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Valve\Steam', 'InstallPath', Result) then
    exit;

  if RegQueryStringValue(HKLM, 'SOFTWARE\Valve\Steam', 'InstallPath', Result) then
    exit;

  if RegQueryStringValue(HKCU, 'Software\Valve\Steam', 'SteamPath', Result) then
    exit;
end;

function DetectGameDir(GameFolderName: String): String;
var
  SteamPath: String;
  Candidate: String;
begin
  Result := '';
  SteamPath := GetSteamInstallPath();

  if SteamPath <> '' then
  begin
    Candidate := CombinePath(SteamPath, 'steamapps\common\' + GameFolderName);
    if DirExists(Candidate) then
    begin
      Result := Candidate;
      exit;
    end;
  end;

  Candidate := ExpandConstant('{pf}\Steam\steamapps\common\' + GameFolderName);
  if DirExists(Candidate) then
  begin
    Result := Candidate;
    exit;
  end;

  Candidate := ExpandConstant('{pf32}\Steam\steamapps\common\' + GameFolderName);
  if DirExists(Candidate) then
  begin
    Result := Candidate;
    exit;
  end;
end;

procedure InitializeWizard();
begin
  TelemetryPage := CreateInputOptionPage(
    wpSelectTasks,
    CustomMessage('TelemetryPageTitle'),
    CustomMessage('TelemetryPageSub'),
    CustomMessage('TelemetryPageDesc'),
    True,
    False
  );

  TelemetryPage.Add(CustomMessage('InstallETS2Plugin'));
  TelemetryPage.Add(CustomMessage('InstallATSPlugin'));

  GameDirPage := CreateInputDirPage(
    TelemetryPage.ID,
    CustomMessage('GameDirPageTitle'),
    CustomMessage('GameDirPageSub'),
    CustomMessage('GameDirPageDesc'),
    False,
    ''
  );

  GameDirPage.Add(CustomMessage('ETS2DirPrompt'));
  GameDirPage.Add(CustomMessage('ATSDirPrompt'));

  GameDirPage.Values[0] := DetectGameDir('Euro Truck Simulator 2');
  GameDirPage.Values[1] := DetectGameDir('American Truck Simulator');

  TelemetryPage.Values[0] := GameDirPage.Values[0] <> '';
  TelemetryPage.Values[1] := GameDirPage.Values[1] <> '';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;

  if PageID = GameDirPage.ID then
    Result := not TelemetryPage.Values[0] and not TelemetryPage.Values[1];
end;

function ValidateGamePath(GamePath: String; ExpectedExe: String): Boolean;
var
  ExpectedPath: String;
begin
  Result := True;

  if GamePath = '' then
  begin
    MsgBox(CustomMessage('PathRequired'), mbError, MB_OK);
    Result := False;
    exit;
  end;

  ExpectedPath := CombinePath(GamePath, 'bin\win_x64\' + ExpectedExe);
  if not FileExists(ExpectedPath) then
  begin
    Result := MsgBox(Format(CustomMessage('PathLooksWrong'), [GamePath, ExpectedPath]), mbConfirmation, MB_YESNO) = IDYES;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = GameDirPage.ID then
  begin
    if TelemetryPage.Values[0] then
      Result := ValidateGamePath(GameDirPage.Values[0], 'eurotrucks2.exe');

    if Result and TelemetryPage.Values[1] then
      Result := ValidateGamePath(GameDirPage.Values[1], 'amtrucks.exe');
  end;
end;

procedure InstallTelemetryPlugin(GameName: String; GamePath: String);
var
  SourceFile: String;
  TargetDir: String;
  TargetFile: String;
begin
  SourceFile := ExpandConstant('{app}\plugin\scs-telemetry.dll');
  TargetDir := CombinePath(GamePath, 'bin\win_x64\plugins');
  TargetFile := CombinePath(TargetDir, 'scs-telemetry.dll');

  if not ForceDirectories(TargetDir) then
  begin
    MsgBox(Format(CustomMessage('PluginInstallFailed'), [GameName, ExpandConstant('{app}\plugin')]), mbError, MB_OK);
    exit;
  end;

  if not FileCopy(SourceFile, TargetFile, False) then
  begin
    MsgBox(Format(CustomMessage('PluginInstallFailed'), [GameName, ExpandConstant('{app}\plugin')]), mbError, MB_OK);
    exit;
  end;

  Log(Format(CustomMessage('PluginInstalled'), [GameName]));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if TelemetryPage.Values[0] then
      InstallTelemetryPlugin('Euro Truck Simulator 2', GameDirPage.Values[0]);

    if TelemetryPage.Values[1] then
      InstallTelemetryPlugin('American Truck Simulator', GameDirPage.Values[1]);
  end;
end;
