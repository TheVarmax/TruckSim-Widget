#define MyAppName "TruckSim Widget"
#define MyAppVersion "1.5.9-beta.2"
#define MyAppExeName "TruckSim Widget.exe"
#define PublishDir "C:\Users\mrpry\Desktop\TruckSim Widget\TruckSim Widget (1.5.9-beta.2)"

[Setup]
AppId={{8F4E6E2C-7F11-4F7D-BD7D-TRUCKSIMWIDGET}
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

english.TelemetryPageTitle=Telemetry Plugin Setup
english.TelemetryPageSub=Configure ETS2 and ATS telemetry automatically.
english.TelemetryPageDesc=TruckSim Widget needs scs-telemetry.dll inside each game's plugins folder. Select the games you want the installer to configure. You can skip this step and install the plugin manually later.
english.InstallETS2Plugin=Install telemetry plugin for Euro Truck Simulator 2
english.InstallATSPlugin=Install telemetry plugin for American Truck Simulator
english.SkipTelemetryPlugin=Skip

english.GameDirPageTitle=Game folders
english.GameDirPageSub=Choose the root folders of your installed games.
english.GameDirPageDesc=Select the game folder that contains bin\win_x64. The installer will create bin\win_x64\plugins if needed and copy scs-telemetry.dll there.
english.ETS2DirPrompt=Euro Truck Simulator 2 folder:
english.ATSDirPrompt=American Truck Simulator folder:
english.PathRequired=Please choose a game folder, or go back and untick this game.
english.PathLooksWrong=This folder does not look like the selected game folder:%n%n%1%n%nExpected file:%n%2%n%nContinue anyway?
english.PluginInstallFailed=Could not install the telemetry plugin for %1.%n%nYou can still copy scs-telemetry.dll manually from:%n%2
english.PluginInstalled=Telemetry plugin installed for %1.

english.CancelSetupTitle=Cancel setup?
english.CancelSetupMessage=TruckSim Widget has not been fully installed yet.%n%nDo you want to cancel setup?
english.CancelSetupYes=Cancel setup
english.CancelSetupNo=Continue installation

english.UpdateWelcome1=Welcome to the TruckSim Widget Update Setup
english.UpdateWelcome2=This will update TruckSim Widget on your computer.%n%nIt is recommended that you close all other applications before continuing.
english.UpdateTitle=TruckSim Widget Update

ukrainian.DesktopIcon=Створити ярлик на робочому столі
ukrainian.AdditionalShortcuts=Додаткові ярлики:
ukrainian.LaunchApp=Запустити TruckSim Widget
ukrainian.OpenPluginFolder=Відкрити папку плагіна телеметрії

ukrainian.TelemetryPageTitle=Налаштування плагіна телеметрії
ukrainian.TelemetryPageSub=Автоматично налаштуй телеметрію для ETS2 та ATS.
ukrainian.TelemetryPageDesc=TruckSim Widget потребує файл scs-telemetry.dll у папці plugins кожної гри. Обери ігри, які інсталятор має налаштувати. Цей крок можна пропустити й встановити плагін вручну пізніше.
ukrainian.InstallETS2Plugin=Встановити плагін телеметрії для Euro Truck Simulator 2
ukrainian.InstallATSPlugin=Встановити плагін телеметрії для American Truck Simulator
ukrainian.SkipTelemetryPlugin=Пропустити

ukrainian.GameDirPageTitle=Папки ігор
ukrainian.GameDirPageSub=Обери кореневі папки встановлених ігор.
ukrainian.GameDirPageDesc=Обери папку гри, у якій є bin\win_x64. Інсталятор створить bin\win_x64\plugins, якщо потрібно, і скопіює туди scs-telemetry.dll.
ukrainian.ETS2DirPrompt=Папка Euro Truck Simulator 2:
ukrainian.ATSDirPrompt=Папка American Truck Simulator:
ukrainian.PathRequired=Обери папку гри або повернися назад і зніми позначку з цієї гри.
ukrainian.PathLooksWrong=Ця папка не схожа на папку вибраної гри:%n%n%1%n%nОчікуваний файл:%n%2%n%nПродовжити все одно?
ukrainian.PluginInstallFailed=Не вдалося встановити плагін телеметрії для %1.%n%nТи все ще можеш скопіювати scs-telemetry.dll вручну з:%n%2
ukrainian.PluginInstalled=Плагін телеметрії встановлено для %1.

ukrainian.CancelSetupTitle=Скасувати встановлення?
ukrainian.CancelSetupMessage=TruckSim Widget ще не встановлено повністю.%n%nСкасувати встановлення?
ukrainian.CancelSetupYes=Скасувати встановлення
ukrainian.CancelSetupNo=Продовжити встановлення

ukrainian.UpdateWelcome1=Ласкаво просимо до оновлення TruckSim Widget
ukrainian.UpdateWelcome2=Ця програма оновить TruckSim Widget на вашому комп'ютері.%n%nРекомендується закрити всі інші програми перед продовженням.
ukrainian.UpdateTitle=Оновлення TruckSim Widget

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalShortcuts}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TruckSim Widget"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:OpenPluginFolder}"; Filename: "{app}\plugin"
Name: "{autodesktop}\TruckSim Widget"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--updated"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent; Check: IsUpdateMode
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent; Check: not IsUpdateMode
[Code]
var
  TelemetryPage: TInputOptionWizardPage;
  GameDirPage: TInputDirWizardPage;
  SkipButton: TNewButton;
  SkipValidation: Boolean;

function IsUpdateMode(): Boolean;
begin
  Result := Pos('--update', GetCmdTail) > 0;
end;

function CombinePath(BasePath: String; RelativePath: String): String;
begin
  Result := AddBackslash(BasePath) + RelativePath;
end;

function GetSteamInstallPath(): String;
begin
  Result := '';

  if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Valve\Steam', 'InstallPath', Result) then exit;
  if RegQueryStringValue(HKLM, 'SOFTWARE\Valve\Steam', 'InstallPath', Result) then exit;
  if RegQueryStringValue(HKCU, 'Software\Valve\Steam', 'SteamPath', Result) then exit;
end;

function TryGameDir(BasePath: String; GameFolderName: String; var FoundPath: String): Boolean;
var
  Candidate: String;
begin
  Result := False;

  if BasePath = '' then
    exit;

  Candidate := CombinePath(BasePath, 'steamapps\common\' + GameFolderName);

  if DirExists(Candidate) then
  begin
    FoundPath := Candidate;
    Result := True;
  end;
end;

function DetectGameDir(GameFolderName: String): String;
var
  SteamPath: String;
begin
  Result := '';

  SteamPath := GetSteamInstallPath();

  if TryGameDir(SteamPath, GameFolderName, Result) then exit;
  if TryGameDir(ExpandConstant('{pf}\Steam'), GameFolderName, Result) then exit;
  if TryGameDir(ExpandConstant('{pf32}\Steam'), GameFolderName, Result) then exit;

  if TryGameDir('D:\SteamLibrary', GameFolderName, Result) then exit;
  if TryGameDir('E:\SteamLibrary', GameFolderName, Result) then exit;
  if TryGameDir('F:\SteamLibrary', GameFolderName, Result) then exit;
  if TryGameDir('G:\SteamLibrary', GameFolderName, Result) then exit;
end;

procedure SkipButtonClick(Sender: TObject);
begin
  if WizardForm.CurPageID = TelemetryPage.ID then
  begin
    TelemetryPage.Values[0] := False;
    TelemetryPage.Values[1] := False;
    WizardForm.NextButton.OnClick(WizardForm.NextButton);
  end
  else if WizardForm.CurPageID = GameDirPage.ID then
  begin
    SkipValidation := True;
    WizardForm.NextButton.OnClick(WizardForm.NextButton);
    SkipValidation := False;
  end;
end;

function IsValidGamePath(GamePath: String; ExpectedExe: String): Boolean;
begin
  Result := (GamePath <> '') and FileExists(CombinePath(GamePath, 'bin\win_x64\' + ExpectedExe));
end;

procedure InitializeWizard();
var
  RegPath: String;
begin
  SkipValidation := False;

  if IsUpdateMode() then
  begin
    WizardForm.Caption := CustomMessage('UpdateTitle');
    WizardForm.WelcomeLabel1.Caption := CustomMessage('UpdateWelcome1');
    WizardForm.WelcomeLabel2.Caption := CustomMessage('UpdateWelcome2');
  end;

  TelemetryPage := CreateInputOptionPage(
    wpSelectTasks,
    CustomMessage('TelemetryPageTitle'),
    CustomMessage('TelemetryPageSub'),
    CustomMessage('TelemetryPageDesc'),
    False,
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
  if RegQueryStringValue(HKCU, 'Software\TruckSim Widget', 'ETS2Path', RegPath) and IsValidGamePath(RegPath, 'eurotrucks2.exe') then
    GameDirPage.Values[0] := RegPath;

  GameDirPage.Values[1] := DetectGameDir('American Truck Simulator');
  if RegQueryStringValue(HKCU, 'Software\TruckSim Widget', 'ATSPath', RegPath) and IsValidGamePath(RegPath, 'amtrucks.exe') then
    GameDirPage.Values[1] := RegPath;

  TelemetryPage.Values[0] := GameDirPage.Values[0] <> '';
  TelemetryPage.Values[1] := GameDirPage.Values[1] <> '';

  SkipButton := TNewButton.Create(WizardForm);
  SkipButton.Parent := WizardForm;
  SkipButton.Caption := CustomMessage('SkipTelemetryPlugin');
  SkipButton.Width := WizardForm.NextButton.Width;
  SkipButton.Height := WizardForm.NextButton.Height;
  SkipButton.Visible := False;
  SkipButton.OnClick := @SkipButtonClick;
end;

procedure CurPageChanged(CurPageID: Integer);
var
  Gap: Integer;
begin
  Gap := ScaleX(8);

  SkipButton.Visible :=
    (CurPageID = TelemetryPage.ID) or
    (CurPageID = GameDirPage.ID);

  if SkipButton.Visible then
  begin
    SkipButton.Top := WizardForm.NextButton.Top;

    SkipButton.Left :=
      WizardForm.NextButton.Left -
      SkipButton.Width -
      Gap;

    WizardForm.BackButton.Left :=
      SkipButton.Left -
      WizardForm.BackButton.Width -
      Gap;
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;

  if IsUpdateMode() then
  begin
    if (PageID = TelemetryPage.ID) or (PageID = GameDirPage.ID) then
    begin
      Result := True;
      exit;
    end;
  end;

  if PageID = GameDirPage.ID then
    Result :=
      ((not TelemetryPage.Values[0]) and
       (not TelemetryPage.Values[1]));
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
    Result :=
      MsgBox(
        Format(CustomMessage('PathLooksWrong'), [GamePath, ExpectedPath]),
        mbConfirmation,
        MB_YESNO
      ) = IDYES;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = GameDirPage.ID then
  begin
    if SkipValidation then
    begin
      if TelemetryPage.Values[0] and not IsValidGamePath(GameDirPage.Values[0], 'eurotrucks2.exe') then
        TelemetryPage.Values[0] := False;

      if TelemetryPage.Values[1] and not IsValidGamePath(GameDirPage.Values[1], 'amtrucks.exe') then
        TelemetryPage.Values[1] := False;

      Result := True;
      exit;
    end;

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
    MsgBox(
      Format(CustomMessage('PluginInstallFailed'), [GameName, ExpandConstant('{app}\plugin')]),
      mbError,
      MB_OK
    );
    exit;
  end;

  if not FileCopy(SourceFile, TargetFile, False) then
  begin
    MsgBox(
      Format(CustomMessage('PluginInstallFailed'), [GameName, ExpandConstant('{app}\plugin')]),
      mbError,
      MB_OK
    );
    exit;
  end;

  Log(Format(CustomMessage('PluginInstalled'), [GameName]));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Install telemetry plugins only if they are checked
    if TelemetryPage.Values[0] then
    begin
      InstallTelemetryPlugin('Euro Truck Simulator 2', GameDirPage.Values[0]);
      RegWriteStringValue(HKCU, 'Software\TruckSim Widget', 'ETS2Path', GameDirPage.Values[0]);
    end;

    if TelemetryPage.Values[1] then
    begin
      InstallTelemetryPlugin('American Truck Simulator', GameDirPage.Values[1]);
      RegWriteStringValue(HKCU, 'Software\TruckSim Widget', 'ATSPath', GameDirPage.Values[1]);
    end;
  end;
end;