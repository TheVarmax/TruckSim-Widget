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

function ExtractNestedJsonValue(const Content, SectionName, KeyName: String): String;
var
  SecPos, EndSecPos: Integer;
  SecBlock: String;
begin
  Result := '';
  SecPos := Pos('"' + SectionName + '"', Content);
  if SecPos > 0 then
  begin
    SecBlock := Copy(Content, SecPos, Length(Content) - SecPos + 1);
    EndSecPos := Pos('}', SecBlock);
    if EndSecPos > 0 then
      SecBlock := Copy(SecBlock, 1, EndSecPos);
    Result := ExtractJsonValue(SecBlock, KeyName);
  end;
end;

function ReadJsonStateFile(var ETS2Config, ATSConfig: TGameConfig): Boolean;
var
  StateFile: String;
  RawContent: AnsiString;
  Content: String;
  IsV3: Boolean;
begin
  Result := False;
  StateFile := GetInstallerStateFilePath();
  if not SafeFileExists(StateFile) then exit;

  if LoadStringFromFile(StateFile, RawContent) then
  begin
    Content := String(RawContent);

    // Canonical v3 has explicit schemaVersion = 3
    IsV3 := (ExtractJsonValue(Content, 'schemaVersion') = '3');

    if IsV3 then
    begin
      LogInfo('Detected canonical v3 install-state schema.');
      // ETS2 state
      ETS2Config.SelectedPath := ExtractNestedJsonValue(Content, 'ETS2', 'gamePath');
      ETS2Config.OwnershipStatus := ExtractNestedJsonValue(Content, 'ETS2', 'ownershipStatus');
      if ETS2Config.OwnershipStatus = '' then
        ETS2Config.OwnershipStatus := ExtractNestedJsonValue(Content, 'ETS2', 'status');
      ETS2Config.ExistingFileHash := ExtractNestedJsonValue(Content, 'ETS2', 'installedPluginHash');
      if ETS2Config.ExistingFileHash = '' then
        ETS2Config.ExistingFileHash := ExtractNestedJsonValue(Content, 'ETS2', 'fileHash');
      ETS2Config.BackupPath := ExtractNestedJsonValue(Content, 'ETS2', 'backupPath');
      if ExtractNestedJsonValue(Content, 'ETS2', 'configured') = 'true' then ETS2Config.UserSelected := True
      else if ExtractNestedJsonValue(Content, 'ETS2', 'configured') = 'false' then ETS2Config.UserSelected := False;

      // ATS state
      ATSConfig.SelectedPath := ExtractNestedJsonValue(Content, 'ATS', 'gamePath');
      ATSConfig.OwnershipStatus := ExtractNestedJsonValue(Content, 'ATS', 'ownershipStatus');
      if ATSConfig.OwnershipStatus = '' then
        ATSConfig.OwnershipStatus := ExtractNestedJsonValue(Content, 'ATS', 'status');
      ATSConfig.ExistingFileHash := ExtractNestedJsonValue(Content, 'ATS', 'installedPluginHash');
      if ATSConfig.ExistingFileHash = '' then
        ATSConfig.ExistingFileHash := ExtractNestedJsonValue(Content, 'ATS', 'fileHash');
      ATSConfig.BackupPath := ExtractNestedJsonValue(Content, 'ATS', 'backupPath');
      if ExtractNestedJsonValue(Content, 'ATS', 'configured') = 'true' then ATSConfig.UserSelected := True
      else if ExtractNestedJsonValue(Content, 'ATS', 'configured') = 'false' then ATSConfig.UserSelected := False;
    end
    else
    begin
      LogInfo('Detected legacy v2 install-state schema. Explicitly migrating to v3 on next save.');
      // Explicit v2 flat schema fallback
      ETS2Config.SelectedPath := ExtractJsonValue(Content, 'ets2_path');
      ETS2Config.OwnershipStatus := ExtractJsonValue(Content, 'ets2_ownership');
      ETS2Config.ExistingFileHash := ExtractJsonValue(Content, 'ets2_hash');
      ETS2Config.BackupPath := ExtractJsonValue(Content, 'ets2_backup');
      if ExtractJsonValue(Content, 'ets2_enabled') = 'true' then ETS2Config.UserSelected := True
      else if ExtractJsonValue(Content, 'ets2_enabled') = 'false' then ETS2Config.UserSelected := False;

      ATSConfig.SelectedPath := ExtractJsonValue(Content, 'ats_path');
      ATSConfig.OwnershipStatus := ExtractJsonValue(Content, 'ats_ownership');
      ATSConfig.ExistingFileHash := ExtractJsonValue(Content, 'ats_hash');
      ATSConfig.BackupPath := ExtractJsonValue(Content, 'ats_backup');
      if ExtractJsonValue(Content, 'ats_enabled') = 'true' then ATSConfig.UserSelected := True
      else if ExtractJsonValue(Content, 'ats_enabled') = 'false' then ATSConfig.UserSelected := False;
    end;

    Result := True;
    LogInfo('Loaded existing installer state from: ' + StateFile);
  end;
end;

function SaveJsonStateFile(const AppVersionStr, AppInstallDir: String; const ETS2Config, ATSConfig: TGameConfig): Boolean;
var
  StateFile: String;
  StateDir: String;
  StagingDir: String;
  BackupFile: String;
  OriginalHash: String;
  StepIdx: Integer;
  Json: String;
  NowIso: String;
begin
  Result := False;
  StateFile := GetInstallerStateFilePath();
  StateDir := ExtractFilePath(StateFile);

  if not SafeDirExists(StateDir) then
    ForceDirectories(StateDir);

  BackupFile := '';
  OriginalHash := '';

  // If install-state.json already exists, stage a rollback backup before modifying it
  if SafeFileExists(StateFile) then
  begin
    OriginalHash := GetFileSha256Safe(StateFile);
    StagingDir := GetTransactionStagingDir();
    if not SafeDirExists(StagingDir) then
      ForceDirectories(StagingDir);

    BackupFile := CombinePath(StagingDir, 'state_rollback_' + GetDateTimeString('yyyymmdd_hhnnss', '', '') + '.json');
    LogInfo('Staging rollback backup of existing install state: ' + StateFile + ' -> ' + BackupFile);
    if not CopyFile(StateFile, BackupFile, False) then
    begin
      if not CopyFileElevated(StateFile, BackupFile) then
      begin
        LogErr('CRITICAL: Failed to stage rollback backup of existing install-state.json!');
        exit;
      end;
    end;
  end;

  // Record transaction journal step: Operation = 'SaveStateFile'
  StepIdx := BeginTransactionStep('SaveStateFile', '', StateFile, BackupFile, OriginalHash);

  NowIso := GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':');

  Json := '{' + #13#10 +
    '  "schemaVersion": 3,' + #13#10 +
    '  "installerVersion": "' + EscapeJson(AppVersionStr) + '",' + #13#10 +
    '  "lastUpdated": "' + EscapeJson(NowIso) + '",' + #13#10 +
    '  "installPath": "' + EscapeJson(AppInstallDir) + '",' + #13#10 +
    '  "games": {' + #13#10 +
    '    "ETS2": {' + #13#10 +
    '      "gameId": "ETS2",' + #13#10 +
    '      "configured": ' + LowerCase(BoolToStr(ETS2Config.UserSelected)) + ',' + #13#10 +
    '      "gamePath": "' + EscapeJson(ETS2Config.SelectedPath) + '",' + #13#10 +
    '      "pluginPath": "' + EscapeJson(ETS2Config.TargetPluginFile) + '",' + #13#10 +
    '      "ownershipStatus": "' + EscapeJson(ETS2Config.OwnershipStatus) + '",' + #13#10 +
    '      "installedPluginHash": "' + EscapeJson(ETS2Config.ExistingFileHash) + '",' + #13#10 +
    '      "backupPath": "' + EscapeJson(ETS2Config.BackupPath) + '"' + #13#10 +
    '    },' + #13#10 +
    '    "ATS": {' + #13#10 +
    '      "gameId": "ATS",' + #13#10 +
    '      "configured": ' + LowerCase(BoolToStr(ATSConfig.UserSelected)) + ',' + #13#10 +
    '      "gamePath": "' + EscapeJson(ATSConfig.SelectedPath) + '",' + #13#10 +
    '      "pluginPath": "' + EscapeJson(ATSConfig.TargetPluginFile) + '",' + #13#10 +
    '      "ownershipStatus": "' + EscapeJson(ATSConfig.OwnershipStatus) + '",' + #13#10 +
    '      "installedPluginHash": "' + EscapeJson(ATSConfig.ExistingFileHash) + '",' + #13#10 +
    '      "backupPath": "' + EscapeJson(ATSConfig.BackupPath) + '"' + #13#10 +
    '    }' + #13#10 +
    '  }' + #13#10 +
    '}' + #13#10;

  Result := AtomicSaveStringToFile(StateFile, Json);
  if Result then
  begin
    LogInfo('Saved canonical v3 install state to: ' + StateFile);
    CompleteTransactionStep(StepIdx);
  end
  else
  begin
    LogErr('Failed to atomically write install state to: ' + StateFile);
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
      LogInfo('[' + Game.GameId + '] Plugin is verified owned and unmodified.');
      Game.OwnershipStatus := OWNERSHIP_OWNED;
      exit;
    end
    else
    begin
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
      // STRICT RULE: matching hash alone != ownership
      // Legacy path-only records without cryptographic metadata remain strictly unverified
      LogWarn('[' + Game.GameId + '] Conservative legacy migration: Marking as LegacyOwnedUnverified.');
      Game.OwnershipStatus := OWNERSHIP_LEGACY_UNVERIFIED;
      exit;
    end;
  end;

  // STRICT RULE: matching hash alone != ownership
  if CompareText(CurrentHash, BundledHash) = 0 then
    LogInfo('[' + Game.GameId + '] Plugin matches bundled hash but has no ownership record (unmanaged/third-party).')
  else
    LogInfo('[' + Game.GameId + '] Third-party plugin detected with different hash.');

  Game.OwnershipStatus := OWNERSHIP_NONE;
end;

function InstallPluginForGame(var Game: TGameConfig; const SourcePluginFile: String): Boolean;
var
  TargetDir: String;
  TargetFile: String;
  BackupTarget: String;
  StagingDir: String;
  StagingBackupFile: String;
  CurrentHash: String;
  Copied: Boolean;
  StepIdx: Integer;
  IsCleanOwnedUpdate: Boolean;
  RollbackRef: String;
begin
  Result := False;
  StagingBackupFile := '';

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
  if not SafeDirExists(TargetDir) then
  begin
    StepIdx := BeginTransactionStep('CreateDir', '', TargetDir, '', '');
    if not ForceDirectories(TargetDir) then
    begin
      if not MkDirElevated(TargetDir) then
      begin
        LogErr('[' + Game.GameId + '] Could not create plugins directory: ' + TargetDir);
        exit;
      end;
    end;
    CompleteTransactionStep(StepIdx);
  end;

  // Handle existing file
  if SafeFileExists(TargetFile) then
  begin
    CurrentHash := GetFileSha256Safe(TargetFile);

    // If user explicitly chose to keep existing
    if Game.ConflictAction = CONFLICT_ACTION_KEEP_EXISTING then
    begin
      LogInfo('[' + Game.GameId + '] User chose to keep existing plugin.');
      Game.OwnershipStatus := OWNERSHIP_SKIPPED;
      Result := True;
      exit;
    end;

    IsCleanOwnedUpdate := (Game.OwnershipStatus = OWNERSHIP_OWNED);

    // ALWAYS create a temporary staging rollback backup before replacing or deleting:
    StagingDir := GetTransactionStagingDir();
    if not SafeDirExists(StagingDir) then ForceDirectories(StagingDir);
    StagingBackupFile := CombinePath(StagingDir, Game.GameId + '_rollback_' + GetDateTimeString('yyyymmdd_hhnnss', '', '') + '.dll');

    LogInfo('[' + Game.GameId + '] Creating temporary staging rollback backup at: ' + StagingBackupFile);
    StepIdx := BeginTransactionStep('StageRollbackBackup', TargetFile, TargetFile, StagingBackupFile, CurrentHash);
    if not CopyFile(TargetFile, StagingBackupFile, False) then
    begin
      if not CopyFileElevated(TargetFile, StagingBackupFile) then
      begin
        LogErr('[' + Game.GameId + '] Failed to create temporary staging rollback backup.');
        exit;
      end;
    end;
    CompleteTransactionStep(StepIdx);

    // ONLY create persistent .trucksim_backup if user EXPLICITLY chose BACKUP_REPLACE:
    if Game.ConflictAction = CONFLICT_ACTION_BACKUP_REPLACE then
    begin
      BackupTarget := CombinePath(TargetDir, PLUGIN_FILENAME + PLUGIN_BACKUP_EXT);
      if SafeFileExists(BackupTarget) then
        BackupTarget := CombinePath(TargetDir, PLUGIN_FILENAME + '.backup_' + GetDateTimeString('yyyymmdd_hhnnss', '', '') + '.bak');

      LogInfo('[' + Game.GameId + '] Creating persistent backup of third-party plugin: ' + BackupTarget);
      StepIdx := BeginTransactionStep('CreateThirdPartyBackup', TargetFile, TargetFile, BackupTarget, CurrentHash);
      if not CopyFile(TargetFile, BackupTarget, False) then
      begin
        if not CopyFileElevated(TargetFile, BackupTarget) then
        begin
          LogErr('[' + Game.GameId + '] Failed to create third-party plugin backup at: ' + BackupTarget);
          exit;
        end;
      end;
      CompleteTransactionStep(StepIdx);

      Game.BackupPath := BackupTarget;
      Game.BackupHash := CurrentHash;
    end
    else
    begin
      // For OVERWRITE and clean Owned update: NO persistent backup in game directory!
      Game.BackupPath := '';
      Game.BackupHash := '';
    end;

    // Delete existing before copy
    if not DeleteFile(TargetFile) then
      DeleteFileElevated(TargetFile);
  end;

  // Copy new plugin file
  RollbackRef := StagingBackupFile;
  if RollbackRef = '' then RollbackRef := Game.BackupPath;

  StepIdx := BeginTransactionStep('CopyPlugin', SourcePluginFile, TargetFile, RollbackRef, CurrentHash);
  Copied := CopyFile(SourcePluginFile, TargetFile, False);
  if not Copied then
  begin
    LogWarn('[' + Game.GameId + '] Standard CopyFile failed. Attempting CopyFileElevated.');
    Copied := CopyFileElevated(SourcePluginFile, TargetFile);
  end;

  if Copied and SafeFileExists(TargetFile) then
  begin
    CompleteTransactionStep(StepIdx);
    Game.ExistingFileHash := GetFileSha256Safe(TargetFile);

    if Game.ConflictAction = CONFLICT_ACTION_BACKUP_REPLACE then
      Game.OwnershipStatus := OWNERSHIP_THIRD_PARTY_REPLACED
    else
      Game.OwnershipStatus := OWNERSHIP_OWNED;

    RegWriteStringValue(HKCU, 'Software\TruckSim Widget', Game.GameId + 'Path', Game.SelectedPath);
    RegWriteStringValue(HKCU, 'Software\TruckSim Widget', Game.GameId + 'PluginOwnedPath', TargetFile);

    LogInfo('[' + Game.GameId + '] Telemetry plugin installed. Status: ' + Game.OwnershipStatus + ', Hash: ' + Game.ExistingFileHash);
    Result := True;
  end
  else
  begin
    LogErr('[' + Game.GameId + '] Failed to copy plugin to: ' + TargetFile);
    Result := False;
  end;
end;
