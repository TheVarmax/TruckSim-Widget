// PluginOwnership.iss - Canonical Ownership Tracking, Hashing, Conflict Resolution & Rollback
// Part of TruckSim Widget Installer

[Code]
var
  RollbackStack: array of TRollbackItem;
  BundledPluginHash: String;

procedure InitRollbackStack();
begin
  SetArrayLength(RollbackStack, 0);
end;

procedure PushRollbackItem(const ActionType: Integer; const TargetPath, BackupPath, OrigHash: String);
var
  Idx: Integer;
begin
  Idx := GetArrayLength(RollbackStack);
  SetArrayLength(RollbackStack, Idx + 1);
  RollbackStack[Idx].ActionType := ActionType;
  RollbackStack[Idx].TargetPath := TargetPath;
  RollbackStack[Idx].BackupPath := BackupPath;
  RollbackStack[Idx].OriginalHash := OrigHash;
  LogDebug('Rollback item recorded: Action=' + IntToStr(ActionType) + ', Target=' + TargetPath);
end;

procedure ExecuteRollback();
var
  I: Integer;
  Item: TRollbackItem;
begin
  LogWarn('Executing transactional rollback of installer actions...');
  for I := GetArrayLength(RollbackStack) - 1 downto 0 do
  begin
    Item := RollbackStack[I];
    try
      case Item.ActionType of
        1: // CreatedFile
          begin
            if SafeFileExists(Item.TargetPath) then
            begin
              DeleteFile(Item.TargetPath);
              LogInfo('Rollback: deleted created file: ' + Item.TargetPath);
            end;
          end;
        2: // BackedUpAndReplaced
          begin
            if SafeFileExists(Item.BackupPath) then
            begin
              CopyFile(Item.BackupPath, Item.TargetPath, False);
              DeleteFile(Item.BackupPath);
              LogInfo('Rollback: restored original file from backup: ' + Item.TargetPath);
            end;
          end;
        3: // CreatedDir
          begin
            if SafeDirExists(Item.TargetPath) then
            begin
              RemoveDir(Item.TargetPath);
              LogInfo('Rollback: removed created empty dir: ' + Item.TargetPath);
            end;
          end;
      end;
    except
      LogErr('Rollback step failed for: ' + Item.TargetPath);
    end;
  end;
  SetArrayLength(RollbackStack, 0);
  LogInfo('Rollback complete.');
end;

function GetFileSha256Safe(const FilePath: String): String;
begin
  Result := '';
  if SafeFileExists(FilePath) then
  begin
    try
      Result := LowerCase(GetSHA256OfFile(FilePath));
    except
      LogErr('Failed to calculate SHA-256 for: ' + FilePath);
      Result := '';
    end;
  end;
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

function ReadJsonStateFile(var ETS2Config, ATSConfig: TGameConfig): Boolean;
var
  StateFile: String;
  RawContent: AnsiString;
  Content: String;
begin
  Result := False;
  StateFile := GetInstallerStateFilePath();
  if not SafeFileExists(StateFile) then exit;

  if LoadStringFromFile(StateFile, RawContent) then
  begin
    Content := String(RawContent);
    // ETS2 state
    ETS2Config.SelectedPath := ExtractJsonValue(Content, 'ets2_path');
    ETS2Config.OwnershipStatus := ExtractJsonValue(Content, 'ets2_ownership');
    ETS2Config.ExistingFileHash := ExtractJsonValue(Content, 'ets2_hash');
    ETS2Config.BackupPath := ExtractJsonValue(Content, 'ets2_backup');
    if ExtractJsonValue(Content, 'ets2_enabled') = 'true' then ETS2Config.UserSelected := True
    else if ExtractJsonValue(Content, 'ets2_enabled') = 'false' then ETS2Config.UserSelected := False;

    // ATS state
    ATSConfig.SelectedPath := ExtractJsonValue(Content, 'ats_path');
    ATSConfig.OwnershipStatus := ExtractJsonValue(Content, 'ats_ownership');
    ATSConfig.ExistingFileHash := ExtractJsonValue(Content, 'ats_hash');
    ATSConfig.BackupPath := ExtractJsonValue(Content, 'ats_backup');
    if ExtractJsonValue(Content, 'ats_enabled') = 'true' then ATSConfig.UserSelected := True
    else if ExtractJsonValue(Content, 'ats_enabled') = 'false' then ATSConfig.UserSelected := False;

    Result := True;
    LogInfo('Loaded existing installer state from: ' + StateFile);
  end;
end;

function SaveJsonStateFile(const AppVersionStr, AppInstallDir: String; const ETS2Config, ATSConfig: TGameConfig): Boolean;
var
  StateFile: String;
  StateDir: String;
  Json: String;
  NowIso: String;
begin
  Result := False;
  StateFile := GetInstallerStateFilePath();
  StateDir := ExtractFilePath(StateFile);

  if not SafeDirExists(StateDir) then
    ForceDirectories(StateDir);

  NowIso := GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':');

  Json := '{' + #13#10 +
    '  "installerVersion": "' + EscapeJson(AppVersionStr) + '",' + #13#10 +
    '  "installDate": "' + EscapeJson(NowIso) + '",' + #13#10 +
    '  "installPath": "' + EscapeJson(AppInstallDir) + '",' + #13#10 +
    '  "ets2_enabled": ' + LowerCase(BoolToStr(ETS2Config.UserSelected)) + ',' + #13#10 +
    '  "ets2_path": "' + EscapeJson(ETS2Config.SelectedPath) + '",' + #13#10 +
    '  "ets2_ownership": "' + EscapeJson(ETS2Config.OwnershipStatus) + '",' + #13#10 +
    '  "ets2_hash": "' + EscapeJson(ETS2Config.ExistingFileHash) + '",' + #13#10 +
    '  "ets2_backup": "' + EscapeJson(ETS2Config.BackupPath) + '",' + #13#10 +
    '  "ats_enabled": ' + LowerCase(BoolToStr(ATSConfig.UserSelected)) + ',' + #13#10 +
    '  "ats_path": "' + EscapeJson(ATSConfig.SelectedPath) + '",' + #13#10 +
    '  "ats_ownership": "' + EscapeJson(ATSConfig.OwnershipStatus) + '",' + #13#10 +
    '  "ats_hash": "' + EscapeJson(ATSConfig.ExistingFileHash) + '",' + #13#10 +
    '  "ats_backup": "' + EscapeJson(ATSConfig.BackupPath) + '"' + #13#10 +
    '}' + #13#10;

  try
    Result := SaveStringToFile(StateFile, Json, False);
    if Result then
      LogInfo('Saved canonical install state to: ' + StateFile)
    else
      LogErr('Failed to write install state to: ' + StateFile);
  except
    LogErr('Exception while saving install state to: ' + StateFile);
    Result := False;
  end;
end;

procedure EvaluatePluginOwnership(var Game: TGameConfig; const BundledHash: String);
var
  PluginFile: String;
  CurrentHash: String;
  LegacyOwnedPath: String;
begin
  if Game.SelectedPath = '' then
  begin
    Game.OwnershipStatus := OWNERSHIP_NONE;
    exit;
  end;

  PluginFile := CombinePath(CombinePath(Game.SelectedPath, GAME_REL_PLUGIN_DIR), PLUGIN_FILENAME);
  Game.TargetPluginFile := PluginFile;
  Game.ExistingFileFound := SafeFileExists(PluginFile);

  if not Game.ExistingFileFound then
  begin
    // Scenario A: File does not exist
    Game.OwnershipStatus := OWNERSHIP_NONE;
    Game.ExistingFileHash := '';
    LogInfo('[' + Game.GameId + '] No existing plugin found at: ' + PluginFile);
    exit;
  end;

  CurrentHash := GetFileSha256Safe(PluginFile);
  LogInfo('[' + Game.GameId + '] Existing plugin found. SHA-256: ' + CurrentHash);

  // Check if we already have recorded ownership in install-state.json
  if Game.OwnershipStatus = OWNERSHIP_OWNED then
  begin
    if (Game.ExistingFileHash <> '') and (CompareText(CurrentHash, Game.ExistingFileHash) = 0) then
    begin
      // Scenario B: Owned and unmodified
      LogInfo('[' + Game.GameId + '] Plugin is owned and unmodified.');
      Game.OwnershipStatus := OWNERSHIP_OWNED;
      exit;
    end
    else
    begin
      // Scenario D: User modified after Widget installation
      LogWarn('[' + Game.GameId + '] Plugin was owned by Widget but hash modified since install.');
      Game.OwnershipStatus := OWNERSHIP_MODIFIED_BY_USER;
      exit;
    end;
  end;

  // Check legacy registry migration
  if RegQueryStringValue(HKCU, 'Software\TruckSim Widget', Game.GameId + 'PluginOwnedPath', LegacyOwnedPath) then
  begin
    if CompareText(NormalizePath(LegacyOwnedPath), NormalizePath(PluginFile)) = 0 then
    begin
      LogInfo('[' + Game.GameId + '] Found legacy path ownership record: ' + LegacyOwnedPath);
      // Path-only legacy ownership cannot be automatically trusted as verified
      if CompareText(CurrentHash, BundledHash) = 0 then
      begin
        LogInfo('[' + Game.GameId + '] Legacy plugin matches bundled hash, adopting as Owned.');
        Game.OwnershipStatus := OWNERSHIP_OWNED;
        Game.ExistingFileHash := CurrentHash;
        exit;
      end
      else
      begin
        LogWarn('[' + Game.GameId + '] Legacy plugin hash differs from bundled. Marking LegacyOwnedUnverified.');
        Game.OwnershipStatus := OWNERSHIP_LEGACY_UNVERIFIED;
        exit;
      end;
    end;
  end;

  // Scenario C: File exists but no ownership record
  // STRICT RULE: matching hash alone != ownership
  if CompareText(CurrentHash, BundledHash) = 0 then
    LogInfo('[' + Game.GameId + '] Plugin matches bundled hash but has no ownership record (unmanaged/third-party).')
  else
    LogInfo('[' + Game.GameId + '] Third-party plugin detected with different hash.');

  Game.OwnershipStatus := OWNERSHIP_NONE; // Unmanaged/Third-party
end;

function InstallPluginForGame(var Game: TGameConfig; const SourcePluginFile: String): Boolean;
var
  TargetDir: String;
  TargetFile: String;
  BackupTarget: String;
  DirCreated: Boolean;
  CurrentHash: String;
  Copied: Boolean;
begin
  Result := False;

  if not Game.UserSelected or (Game.SelectedPath = '') then
  begin
    LogInfo('[' + Game.GameId + '] Plugin installation skipped by user preference.');
    Game.OwnershipStatus := OWNERSHIP_SKIPPED;
    Result := True;
    exit;
  end;

  TargetDir := CombinePath(Game.SelectedPath, GAME_REL_PLUGIN_DIR);
  TargetFile := CombinePath(TargetDir, PLUGIN_FILENAME);
  Game.TargetPluginFile := TargetFile;

  LogInfo('[' + Game.GameId + '] Installing telemetry plugin to: ' + TargetFile);

  // Check if directory exists
  DirCreated := False;
  if not SafeDirExists(TargetDir) then
  begin
    if not ForceDirectories(TargetDir) then
    begin
      LogErr('[' + Game.GameId + '] Could not create plugins directory: ' + TargetDir);
      exit;
    end;
    DirCreated := True;
    PushRollbackItem(3, TargetDir, '', '');
  end;

  // Handle existing file / conflict resolution
  if SafeFileExists(TargetFile) then
  begin
    CurrentHash := GetFileSha256Safe(TargetFile);

    // If keeping existing
    if Game.ConflictAction = CONFLICT_ACTION_KEEP_EXISTING then
    begin
      LogInfo('[' + Game.GameId + '] User chose to keep existing plugin.');
      Game.OwnershipStatus := OWNERSHIP_SKIPPED;
      Result := True;
      exit;
    end;

    // If backup and replace
    if (Game.ConflictAction = CONFLICT_ACTION_BACKUP_REPLACE) or
       (Game.OwnershipStatus = OWNERSHIP_LEGACY_UNVERIFIED) or
       (Game.OwnershipStatus = OWNERSHIP_MODIFIED_BY_USER) or
       (Game.OwnershipStatus = OWNERSHIP_NONE) then
    begin
      BackupTarget := CombinePath(TargetDir, PLUGIN_FILENAME + PLUGIN_BACKUP_EXT);
      if SafeFileExists(BackupTarget) then
      begin
        // Do not overwrite existing backup; create timestamped backup
        BackupTarget := CombinePath(TargetDir, PLUGIN_FILENAME + '.backup_' + GetDateTimeString('yyyymmdd_hhnnss', '', '') + '.bak');
      end;

      LogInfo('[' + Game.GameId + '] Backing up existing plugin to: ' + BackupTarget);
      if not CopyFile(TargetFile, BackupTarget, False) then
      begin
        LogErr('[' + Game.GameId + '] Failed to create plugin backup at: ' + BackupTarget);
        exit;
      end;

      Game.BackupPath := BackupTarget;
      Game.BackupHash := CurrentHash;
      PushRollbackItem(2, TargetFile, BackupTarget, CurrentHash);
    end;

    // Delete existing before copy
    if not DeleteFile(TargetFile) then
    begin
      LogWarn('[' + Game.GameId + '] DeleteFile failed on target file. Trying elevated copy.');
    end;
  end
  else
  begin
    PushRollbackItem(1, TargetFile, '', '');
  end;

  // Copy new plugin file
  Copied := CopyFile(SourcePluginFile, TargetFile, False);
  if not Copied then
  begin
    // Attempt targeted elevated copy if standard copy failed
    LogWarn('[' + Game.GameId + '] Standard CopyFile failed. Attempting CopyFileElevated.');
    Copied := CopyFileElevated(SourcePluginFile, TargetFile);
  end;

  if Copied and SafeFileExists(TargetFile) then
  begin
    Game.ExistingFileHash := GetFileSha256Safe(TargetFile);
    if Game.BackupPath <> '' then
      Game.OwnershipStatus := OWNERSHIP_THIRD_PARTY_REPLACED
    else
      Game.OwnershipStatus := OWNERSHIP_OWNED;

    // Mirror to HKCU registry for compatibility
    RegWriteStringValue(HKCU, 'Software\TruckSim Widget', Game.GameId + 'Path', Game.SelectedPath);
    RegWriteStringValue(HKCU, 'Software\TruckSim Widget', Game.GameId + 'PluginOwnedPath', TargetFile);

    LogInfo('[' + Game.GameId + '] Telemetry plugin successfully installed. State: ' + Game.OwnershipStatus + ', Hash: ' + Game.ExistingFileHash);
    Result := True;
  end
  else
  begin
    LogErr('[' + Game.GameId + '] Failed to copy scs-telemetry.dll to: ' + TargetFile);
    Result := False;
  end;
end;
