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
  CONFLICT_ACTION_NONE           = 0;
  CONFLICT_ACTION_BACKUP_REPLACE = 1;
  CONFLICT_ACTION_KEEP_EXISTING  = 2;
  CONFLICT_ACTION_OVERWRITE      = 3;

  // Transaction Journal statuses
  TRANSACTION_STATUS_PENDING     = 'PENDING';
  TRANSACTION_STATUS_COMMITTED   = 'COMMITTED';
  TRANSACTION_STATUS_ROLLED_BACK = 'ROLLED_BACK';
  STEP_STATUS_PENDING            = 'STEP_PENDING';
  STEP_STATUS_COMPLETED          = 'STEP_COMPLETED';

  // Drive types from Win32
  DRIVE_FIXED = 3;

  // Required disk space in MB
  REQUIRED_FREE_SPACE_MB = 250;

  // Expected ElevatedHelper SHA-256 hash computed at compile time
  EXPECTED_HELPER_SHA256 = '{#ElevatedHelperSha256}';

var
  IsUninstallMode: Boolean;

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

function EscapeJson(const S: String): String;
var
  Res: String;
begin
  Res := S;
  StringChange(Res, '\', '\\');
  StringChange(Res, '"', '\"');
  Result := Res;
end;

function ExtractJsonValue(const Content, Key: String): String;
var
  KeyPat: String;
  PosKey, StartPos: Integer;
begin
  Result := '';
  KeyPat := '"' + Key + '"';
  PosKey := Pos(KeyPat, Content);
  if PosKey > 0 then
  begin
    PosKey := PosKey + Length(KeyPat);
    while (PosKey <= Length(Content)) and (Content[PosKey] in [' ', ':', #9, #13, #10]) do
      PosKey := PosKey + 1;
    if (PosKey <= Length(Content)) and (Content[PosKey] = '"') then
    begin
      PosKey := PosKey + 1;
      StartPos := PosKey;
      while (PosKey <= Length(Content)) and (Content[PosKey] <> '"') do
        PosKey := PosKey + 1;
      Result := Copy(Content, StartPos, PosKey - StartPos);
      StringChange(Result, '\\', '\');
    end
    else if (PosKey <= Length(Content)) and ((Content[PosKey] = 't') or (Content[PosKey] = 'f')) then
    begin
      if Copy(Content, PosKey, 4) = 'true' then Result := 'true'
      else if Copy(Content, PosKey, 5) = 'false' then Result := 'false';
    end;
  end;
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

function GetFileSha256Safe(const FilePath: String): String;
begin
  Result := '';
  if SafeFileExists(FilePath) then
  begin
    try
      Result := LowerCase(GetSHA256OfFile(FilePath));
    except
      Result := '';
    end;
  end;
end;

function VerifyElevatedHelperIntegrity(const HelperPath: String): Boolean;
var
  ActualHash: String;
begin
  Result := False;
  if not SafeFileExists(HelperPath) then exit;
  ActualHash := GetFileSha256Safe(HelperPath);
  Result := (CompareText(ActualHash, EXPECTED_HELPER_SHA256) = 0);
end;

function AtomicSaveStringToFile(const FilePath, Content: String): Boolean;
var
  TempPath: String;
begin
  Result := False;
  TempPath := FilePath + '.tmp';
  if not SaveStringToFile(TempPath, Content, False) then exit;

  if SafeFileExists(FilePath) then
  begin
    if not DeleteFile(FilePath) then
    begin
      if CopyFile(TempPath, FilePath, False) then
      begin
        DeleteFile(TempPath);
        Result := True;
        exit;
      end;
      exit;
    end;
  end;

  Result := RenameFile(TempPath, FilePath);
  if not Result then
  begin
    Result := CopyFile(TempPath, FilePath, False);
    if Result then DeleteFile(TempPath);
  end;
end;

function GetTransactionJournalFilePath(): String;
begin
  Result := CombinePath(CombinePath(GetAppDataWidgetDir(), 'installer'), 'transaction_journal.json');
end;

function GetTransactionStagingDir(): String;
begin
  Result := CombinePath(CombinePath(GetAppDataWidgetDir(), 'installer'), 'staging');
end;

function GetElevatedHelperPath(): String;
var
  TmpHelper: String;
  AppHelper: String;
begin
  TmpHelper := ExpandConstant('{tmp}\ElevatedHelper.exe');
  AppHelper := ExpandConstant('{app}\ElevatedHelper.exe');
  
  if SafeFileExists(TmpHelper) then
    Result := TmpHelper
  else if IsUninstallMode and SafeFileExists(AppHelper) then
  begin
    if VerifyElevatedHelperIntegrity(AppHelper) then
      Result := AppHelper
    else
      Result := '';
  end
  else
    Result := TmpHelper;
end;
