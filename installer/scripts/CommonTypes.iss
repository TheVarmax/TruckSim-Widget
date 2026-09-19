// CommonTypes.iss - Shared constants, records, and utility functions
// Part of TruckSim Widget Installer

[Code]
const
  // Mutex identifiers
  APP_MUTEX_NAME = 'TruckSim_Widget_SingleInstance_Mutex';
  INSTALLER_MUTEX_NAME = 'TruckSim_Widget_Installer_Mutex';

  // Game identifiers
  GAME_ETS2 = 'ETS2';
  GAME_ATS  = 'ATS';

  // Steam App IDs
  STEAM_APPID_ETS2 = 227300;
  STEAM_APPID_ATS  = 270880;

  // Executable names
  GAME_EXE_ETS2 = 'eurotrucks2.exe';
  GAME_EXE_ATS  = 'amtrucks.exe';

  // Default Steam game folder names
  GAME_DIRNAME_ETS2 = 'Euro Truck Simulator 2';
  GAME_DIRNAME_ATS  = 'American Truck Simulator';

  // Relative path to win_x64 plugins
  GAME_REL_PLUGIN_DIR = 'bin\win_x64\plugins';
  GAME_REL_BIN_DIR    = 'bin\win_x64';
  PLUGIN_FILENAME     = 'scs-telemetry.dll';
  PLUGIN_BACKUP_EXT   = '.trucksim_backup';

  // Plugin Ownership States
  OWNERSHIP_NONE                = 'None';
  OWNERSHIP_OWNED               = 'Owned';
  OWNERSHIP_LEGACY_UNVERIFIED   = 'LegacyOwnedUnverified';
  OWNERSHIP_THIRD_PARTY_REPLACED = 'ThirdPartyReplaced';
  OWNERSHIP_SKIPPED             = 'Skipped';
  OWNERSHIP_MODIFIED_BY_USER    = 'ModifiedByUser';

  // Conflict actions chosen by user
  CONFLICT_ACTION_BACKUP_REPLACE = 1;
  CONFLICT_ACTION_KEEP_EXISTING  = 2;
  CONFLICT_ACTION_OVERWRITE      = 3;

  // Drive types from Win32
  DRIVE_FIXED = 3;

  // Required disk space in MB
  REQUIRED_FREE_SPACE_MB = 250;

type
  TGameConfig = record
    GameId: String;          // 'ETS2' or 'ATS'
    DisplayName: String;     // 'Euro Truck Simulator 2' or 'American Truck Simulator'
    ExeName: String;         // 'eurotrucks2.exe' or 'amtrucks.exe'
    DefaultFolderName: String;
    SteamAppId: Integer;
    UserSelected: Boolean;   // Whether user wants telemetry plugin installed
    DetectedPath: String;    // Auto-detected path
    SelectedPath: String;    // Path chosen by user
    TargetPluginFile: String;// <SelectedPath>\bin\win_x64\plugins\scs-telemetry.dll
    ExistingFileFound: Boolean;
    ExistingFileHash: String;
    OwnershipStatus: String; // OWNERSHIP_*
    ConflictAction: Integer; // CONFLICT_ACTION_*
    BackupPath: String;
    BackupHash: String;
    NeedsElevation: Boolean; // Game dir requires admin privileges
    IsRunning: Boolean;      // Game process currently active
  end;

  TRollbackItem = record
    ActionType: Integer; // 1 = CreatedFile, 2 = BackedUpAndReplaced, 3 = CreatedDir
    TargetPath: String;
    BackupPath: String;
    OriginalHash: String;
  end;

function BoolToStr(const B: Boolean): String;
begin
  if B then
    Result := 'True'
  else
    Result := 'False';
end;

function NormalizePath(const P: String): String;
var
  S: String;
begin
  S := Trim(P);
  StringChange(S, '/', '\');
  while (Length(S) > 0) and (S[Length(S)] = '\') and (Length(S) > 3) do
    Delete(S, Length(S), 1);
  Result := S;
end;

function CombinePath(const BasePath, RelPath: String): String;
begin
  if BasePath = '' then
    Result := RelPath
  else if RelPath = '' then
    Result := BasePath
  else
    Result := AddBackslash(BasePath) + RelPath;
end;

function IsUpdateMode(): Boolean;
begin
  Result := Pos('--update', LowerCase(GetCmdTail)) > 0;
end;

function GetAppDataWidgetDir(): String;
begin
  Result := CombinePath(ExpandConstant('{localappdata}'), 'TruckSimWidget');
end;

function GetInstallerStateFilePath(): String;
begin
  Result := CombinePath(CombinePath(GetAppDataWidgetDir(), 'installer'), 'install-state.json');
end;

function GetInstallerLogFilePath(): String;
begin
  Result := CombinePath(CombinePath(GetAppDataWidgetDir(), 'installer'), 'installer_log.txt');
end;

function GetCanonicalStateDatPath(): String;
begin
  Result := CombinePath(GetAppDataWidgetDir(), 'state.dat');
end;

function SafeFileExists(const Path: String): Boolean;
begin
  Result := (Trim(Path) <> '') and FileExists(Path);
end;

function SafeDirExists(const Path: String): Boolean;
begin
  Result := (Trim(Path) <> '') and DirExists(Path);
end;
