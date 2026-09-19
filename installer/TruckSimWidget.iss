// TruckSim Widget Installer Script
// Overhauled Architecture with Modular Lifecycle, Steam Discovery, and Plugin Ownership

#define MyAppName "TruckSim Widget"
#ifndef MyAppVersion
  #define MyAppVersion "1.6.4-beta.1"
#endif
#define MyAppExeName "TruckSim Widget.exe"

#ifndef PublishDir
  #define PublishDir "C:\Users\mrpry\Desktop\TruckSim Widget\TruckSim Widget (" + MyAppVersion + ")"
#endif
#define ElevatedHelperFile AddBackslash(PublishDir) + "ElevatedHelper.exe"
#define ElevatedHelperSha256 LowerCase(GetSHA256OfFile(ElevatedHelperFile))
#ifndef OutputDir
  #define OutputDir "C:\Users\mrpry\Desktop\TruckSim Widget\Releases"
#endif

[Setup]
AppId={{8F4E6E2C-7F11-4F7D-BD7D-TRUCKSIMWIDGET}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=TheVarmax
AppPublisherURL=https://trucksim.uk
AppSupportURL=https://trucksim.uk
AppUpdatesURL=https://github.com/TheVarmax/TruckSim-Widget/releases
DefaultDirName={localappdata}\Programs\TruckSim Widget
DefaultGroupName=TruckSim Widget
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=TruckSimWidgetSetup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
SetupIconFile=..\favicon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
LZMANumBlockThreads=8
AppMutex=TruckSim_Widget_SingleInstance_Mutex
SetupMutex=TruckSim_Widget_Installer_Mutex
SetupLogging=yes
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[CustomMessages]
english.DesktopIcon=Create a desktop shortcut
english.AdditionalShortcuts=Additional shortcuts:
english.LaunchApp=Launch TruckSim Widget
english.OpenPluginFolder=Open telemetry plugin folder
english.ErrElevatedHelperCorrupt=Security error: ElevatedHelper.exe failed integrity verification. Installation aborted.
ukrainian.ErrElevatedHelperCorrupt=Помилка безпеки: ElevatedHelper.exe не пройшов перевірку цілісності. Встановлення скасовано.

english.GameNameETS2=Euro Truck Simulator 2
english.GameNameATS=American Truck Simulator

english.TelemetryPageTitle=Telemetry Plugin Setup
english.TelemetryPageSub=Configure ETS2 and ATS telemetry automatically.
english.TelemetryPagePrompt=TruckSim Widget requires scs-telemetry.dll inside each game's plugins folder. Select the games you want the installer to configure:
english.TelemetryPageDesc=TruckSim Widget requires scs-telemetry.dll inside each game's plugins folder. Select the games you want the installer to configure:
english.InstallETS2Plugin=Configure telemetry plugin for Euro Truck Simulator 2
english.InstallATSPlugin=Configure telemetry plugin for American Truck Simulator

english.GameDirPageTitle=Game Folders
english.GameDirPageSub=Verify or choose the root folders of your installed games.
english.ETS2DirPrompt=Euro Truck Simulator 2 folder:
english.ATSDirPrompt=American Truck Simulator folder:
english.BrowseBtn=Browse...
english.BrowseETS2Title=Select Euro Truck Simulator 2 Installation Folder
english.BrowseATSTitle=Select American Truck Simulator Installation Folder

english.ConflictPageTitle=Telemetry Plugin Conflict Resolution
english.ConflictPageSub=A telemetry plugin is already present in one or more game folders.
english.ConflictHeader=Attention: Existing Plugin Detected
english.ConflictDesc=An existing or third-party telemetry plugin was found. Choose how you want the installer to handle it:
english.ConflictOptionBackup=Backup existing plugin and install TruckSim Widget plugin (Recommended)
english.ConflictOptionKeep=Keep existing plugin (skip installing Widget plugin for that game)
english.ConflictOptionOverwrite=Overwrite existing plugin without backup

english.ErrPathEmpty=Please select a game folder, or uncheck this game in the previous step.
english.ErrDirNotExist=The specified folder does not exist:%n%n%1
english.ErrBinDirMissing=This folder does not contain bin\win_x64:%n%n%1
english.ErrExeMissing=Expected game executable was not found in bin\win_x64:%n%n%1\%2
english.ErrGameRunningPrompt=%1 is currently running (%2). You cannot install or update the telemetry plugin while the game is open.%n%nClick [Yes] to Retry after closing the game.%nClick [No] to Skip plugin setup for %1.%nClick [Cancel] to Abort setup.
english.ErrTargetDirNotWritable=The selected installation directory is not writable. Please choose another location.
english.ErrPluginInstallRollback=Could not complete telemetry plugin installation. Actions were rolled back to preserve system consistency.

english.SummaryDestDir=Destination folder:
english.SummaryPlugins=Telemetry plugin setup:
english.SummarySkipped=Skipped by user
english.SummaryConflictAction=Conflict resolution:
english.UninstallPromptUserData=Do you want to completely remove your user data (settings, logs, trip history, and license token)?%n%nClick 'No' to preserve your settings and data for future use.

english.UpdateWelcome1=Welcome to the TruckSim Widget Update Setup
english.UpdateWelcome2=This will update TruckSim Widget on your computer.%n%nYour settings, license token, and trip history will be preserved.
english.UpdateTitle=TruckSim Widget Update

ukrainian.DesktopIcon=Створити ярлик на робочому столі
ukrainian.AdditionalShortcuts=Додаткові ярлики:
ukrainian.LaunchApp=Запустити TruckSim Widget
ukrainian.OpenPluginFolder=Відкрити папку плагіна телеметрії

ukrainian.GameNameETS2=Euro Truck Simulator 2
ukrainian.GameNameATS=American Truck Simulator

ukrainian.TelemetryPageTitle=Налаштування плагіна телеметрії
ukrainian.TelemetryPageSub=Автоматичне налаштування телеметрії для ETS2 та ATS.
ukrainian.TelemetryPagePrompt=TruckSim Widget потребує scs-telemetry.dll у папці plugins кожної гри. Обери ігри для налаштування:
ukrainian.TelemetryPageDesc=TruckSim Widget потребує scs-telemetry.dll у папці plugins кожної гри. Обери ігри для налаштування:
ukrainian.InstallETS2Plugin=Налаштувати плагін телеметрії для Euro Truck Simulator 2
ukrainian.InstallATSPlugin=Налаштувати плагін телеметрії для American Truck Simulator

ukrainian.GameDirPageTitle=Папки ігор
ukrainian.GameDirPageSub=Перевір або обери кореневі папки встановлених ігор.
ukrainian.ETS2DirPrompt=Папка Euro Truck Simulator 2:
ukrainian.ATSDirPrompt=Папка American Truck Simulator:
ukrainian.BrowseBtn=Огляд...
ukrainian.BrowseETS2Title=Оберіть папку Euro Truck Simulator 2
ukrainian.BrowseATSTitle=Оберіть папку American Truck Simulator

ukrainian.ConflictPageTitle=Вирішення конфлікту плагіна телеметрії
ukrainian.ConflictPageSub=Плагін телеметрії вже присутній у папці гри.
ukrainian.ConflictHeader=Увага: виявлено існуючий плагін
ukrainian.ConflictDesc=Виявлено сторонній або раніше встановлений плагін телеметрії. Обери дію інсталятора:
ukrainian.ConflictOptionBackup=Створити резервну копію та встановити плагін TruckSim Widget (Рекомендовано)
ukrainian.ConflictOptionKeep=Залишити поточний плагін (пропустити встановлення для цієї гри)
ukrainian.ConflictOptionOverwrite=Перезаписати поточний плагін без резервної копії

ukrainian.ErrPathEmpty=Будь ласка, оберіть папку гри або поверніться назад і зніміть позначку.
ukrainian.ErrDirNotExist=Вказана папка не існує:%n%n%1
ukrainian.ErrBinDirMissing=Ця папка не містить bin\win_x64:%n%n%1
ukrainian.ErrExeMissing=Очікуваний файл гри не знайдено в bin\win_x64:%n%n%1\%2
ukrainian.ErrGameRunningPrompt=%1 зараз запущено (%2). Неможливо встановити або оновити плагін телеметрії під час роботи гри.%n%nНатисніть [Так], щоб повторити після закриття гри.%nНатисніть [Ні], щоб пропустити встановлення для %1.%nНатисніть [Скасувати], щоб зупинити встановлення.
ukrainian.ErrTargetDirNotWritable=Цільова папка недоступна для запису. Будь ласка, оберіть інше розташування.
ukrainian.ErrPluginInstallRollback=Не вдалося завершити встановлення плагіна телеметрії. Зміни скасовано для збереження цілісності системи.

ukrainian.SummaryDestDir=Папка встановлення:
ukrainian.SummaryPlugins=Налаштування плагінів телеметрії:
ukrainian.SummarySkipped=Пропущено користувачем
ukrainian.SummaryConflictAction=Вирішення конфліктів:
ukrainian.UninstallPromptUserData=Ви бажаєте повністю видалити дані користувача (налаштування, логи, історію рейсів та ліцензійний токен)?%n%nНатисніть «Ні», щоб зберегти ваші налаштування для майбутнього використання.

ukrainian.UpdateWelcome1=Ласкаво просимо до оновлення TruckSim Widget
ukrainian.UpdateWelcome2=Ця програма оновить TruckSim Widget на вашому комп'ютері.%n%nВаші налаштування, ліцензія та історія рейсів будуть збережені.
ukrainian.UpdateTitle=Оновлення TruckSim Widget

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalShortcuts}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishDir}\ElevatedHelper.exe"; DestDir: "{tmp}"; Flags: dontcopy

[Icons]
Name: "{group}\TruckSim Widget"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:OpenPluginFolder}"; Filename: "{app}\plugin"
Name: "{autodesktop}\TruckSim Widget"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--updated"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent; Check: IsUpdateMode
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent; Check: not IsUpdateMode

[UninstallDelete]
Type: dirifempty; Name: "{app}\Resources"

[Code]
#include "scripts\CommonTypes.iss"
#include "scripts\Diagnostics.iss"
#include "scripts\GameValidation.iss"
#include "scripts\TransactionJournal.iss"
#include "scripts\PluginOwnership.iss"
#include "scripts\SteamDetection.iss"
#include "scripts\WizardPages.iss"
#include "scripts\UninstallLogic.iss"

function InitializeSetup(): Boolean;
var
  TmpHelper: String;
begin
  Result := True;
  InitInstallerLogging('{#MyAppVersion}');
  ExtractTemporaryFile('ElevatedHelper.exe');
  TmpHelper := ExpandConstant('{tmp}\ElevatedHelper.exe');
  if not VerifyElevatedHelperIntegrity(TmpHelper) then
  begin
    LogErr('ElevatedHelper.exe failed SHA-256 integrity verification after extraction! Aborting.');
    MsgBox(CustomMessage('ErrElevatedHelperCorrupt'), mbCriticalError, MB_OK);
    Result := False;
    exit;
  end;
  CheckAndExecuteCrashRecovery();
end;

procedure InitializeWizard();
var
  ETS2Candidates, ATSCandidates: TArrayOfString;
  RegVal: String;
  HasSavedState: Boolean;
begin
  ApplyWidgetTheme();

  // Initialize Game Config records
  GlobalETS2Config.GameId := GAME_ETS2;
  GlobalETS2Config.DisplayName := 'Euro Truck Simulator 2';
  GlobalETS2Config.ExeName := GAME_EXE_ETS2;
  GlobalETS2Config.DefaultFolderName := GAME_DIRNAME_ETS2;
  GlobalETS2Config.SteamAppId := STEAM_APPID_ETS2;
  GlobalETS2Config.ConflictAction := CONFLICT_ACTION_NONE;

  GlobalATSConfig.GameId := GAME_ATS;
  GlobalATSConfig.DisplayName := 'American Truck Simulator';
  GlobalATSConfig.ExeName := GAME_EXE_ATS;
  GlobalATSConfig.DefaultFolderName := GAME_DIRNAME_ATS;
  GlobalATSConfig.SteamAppId := STEAM_APPID_ATS;
  GlobalATSConfig.ConflictAction := CONFLICT_ACTION_NONE;

  // Detect previous install dir
  InitialUpdateDir := '';
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F4E6E2C-7F11-4F7D-BD7D-TRUCKSIMWIDGET}_is1', 'InstallLocation', RegVal) then
    InitialUpdateDir := NormalizePath(RegVal);

  // Read existing canonical state if present
  HasSavedState := ReadJsonStateFile(GlobalETS2Config, GlobalATSConfig);

  // Perform multi-stage discovery if no previous state was found
  if not HasSavedState or (GlobalETS2Config.SelectedPath = '') then
  begin
    GlobalETS2Config.DetectedPath := DetectGameInstallation(GAME_ETS2, STEAM_APPID_ETS2, GAME_EXE_ETS2, GAME_DIRNAME_ETS2, ETS2Candidates);
    if GlobalETS2Config.DetectedPath <> '' then
    begin
      GlobalETS2Config.SelectedPath := GlobalETS2Config.DetectedPath;
      GlobalETS2Config.UserSelected := True;
      LogInfo('Pre-selected discovered ETS2 path: ' + GlobalETS2Config.DetectedPath);
    end;
  end;

  if not HasSavedState or (GlobalATSConfig.SelectedPath = '') then
  begin
    GlobalATSConfig.DetectedPath := DetectGameInstallation(GAME_ATS, STEAM_APPID_ATS, GAME_EXE_ATS, GAME_DIRNAME_ATS, ATSCandidates);
    if GlobalATSConfig.DetectedPath <> '' then
    begin
      GlobalATSConfig.SelectedPath := GlobalATSConfig.DetectedPath;
      GlobalATSConfig.UserSelected := True;
      LogInfo('Pre-selected discovered ATS path: ' + GlobalATSConfig.DetectedPath);
    end;
  end;

  // Create wizard controls
  CreateGameSelectionControls();
  CreateConflictResolutionControls();

  // Populate edit controls
  if GlobalETS2Config.SelectedPath <> '' then
    ETS2PathEdit.Text := GlobalETS2Config.SelectedPath;
  if GlobalATSConfig.SelectedPath <> '' then
    ATSPathEdit.Text := GlobalATSConfig.SelectedPath;

  if IsUpdateMode() then
  begin
    WizardForm.Caption := CustomMessage('UpdateTitle');
    WizardForm.WelcomeLabel1.Caption := CustomMessage('UpdateWelcome1');
    WizardForm.WelcomeLabel2.Caption := CustomMessage('UpdateWelcome2');
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := WizardShouldSkipPage(PageID);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := HandleNextButtonClick(CurPageID);
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := BuildReadySummary();
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  SourcePluginFile: String;
  InstallSuccess: Boolean;
begin
  if CurStep = ssPostInstall then
  begin
    SourcePluginFile := CombinePath(CombinePath(WizardDirValue(), 'plugin'), PLUGIN_FILENAME);
    BundledPluginHash := GetFileSha256Safe(SourcePluginFile);
    LogInfo('Bundled plugin SHA-256: ' + BundledPluginHash);

    InitRollbackStack();
    InitTransactionJournal();
    InstallSuccess := True;

    // ETS2 plugin installation
    if GlobalETS2Config.UserSelected then
    begin
      if not InstallPluginForGame(GlobalETS2Config, SourcePluginFile) then
        InstallSuccess := False;
    end;

    // ATS plugin installation
    if InstallSuccess and GlobalATSConfig.UserSelected then
    begin
      if not InstallPluginForGame(GlobalATSConfig, SourcePluginFile) then
        InstallSuccess := False;
    end;

    // Save canonical v3 state inside transaction boundary BEFORE commit
    if InstallSuccess then
    begin
      if not SaveJsonStateFile('{#MyAppVersion}', WizardDirValue(), GlobalETS2Config, GlobalATSConfig) then
      begin
        LogErr('CRITICAL: Failed to save install state! Initiating transaction rollback.');
        InstallSuccess := False;
      end;
    end;

    if not InstallSuccess then
    begin
      LogErr('Installation encountered an error. Initiating transaction rollback.');
      RollbackTransaction();
      ExecuteRollback();
      MsgBox(CustomMessage('ErrPluginInstallRollback'), mbError, MB_OK);
      Abort;
    end
    else
    begin
      CommitTransaction();
      LogInfo('Installation, plugin setup, and state persistence completed successfully.');
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    ExecuteUninstallCleanup();
  end;
end;