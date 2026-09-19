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

    // Check if canonical v3 schema is present
    IsV3 := (Pos('"games"', Content) > 0) and (Pos('"ETS2"', Content) > 0);

    if IsV3 then
    begin
      LogInfo('Detected canonical v3 install-state schema.');
      // ETS2 state
      ETS2Config.SelectedPath := ExtractNestedJsonValue(Content, 'ETS2', 'gamePath');
      ETS2Config.OwnershipStatus := ExtractNestedJsonValue(Content, 'ETS2', 'status');
      ETS2Config.ExistingFileHash := ExtractNestedJsonValue(Content, 'ETS2', 'fileHash');
      ETS2Config.BackupPath := ExtractNestedJsonValue(Content, 'ETS2', 'backupPath');
      if ExtractNestedJsonValue(Content, 'ETS2', 'configured') = 'true' then ETS2Config.UserSelected := True
      else if ExtractNestedJsonValue(Content, 'ETS2', 'configured') = 'false' then ETS2Config.UserSelected := False;

      // ATS state
      ATSConfig.SelectedPath := ExtractNestedJsonValue(Content, 'ATS', 'gamePath');
      ATSConfig.OwnershipStatus := ExtractNestedJsonValue(Content, 'ATS', 'status');
      ATSConfig.ExistingFileHash := ExtractNestedJsonValue(Content, 'ATS', 'fileHash');
      ATSConfig.BackupPath := ExtractNestedJsonValue(Content, 'ATS', 'backupPath');
      if ExtractNestedJsonValue(Content, 'ATS', 'configured') = 'true' then ATSConfig.UserSelected := True
      else if ExtractNestedJsonValue(Content, 'ATS', 'configured') = 'false' then ATSConfig.UserSelected := False;
    end
    else
    begin
      LogInfo('Detected legacy v2 install-state schema. Migrating to v3 on next save.');
      // Backward-compatible v2 flat schema fallback
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
    '  "games": {' + #13#10 +
    '    "ETS2": {' + #13#10 +
    '      "configured": ' + LowerCase(BoolToStr(ETS2Config.UserSelected)) + ',' + #13#10 +
    '      "gamePath": "' + EscapeJson(ETS2Config.SelectedPath) + '",' + #13#10 +
    '      "pluginPath": "' + EscapeJson(ETS2Config.TargetPluginFile) + '",' + #13#10 +
    '      "status": "' + EscapeJson(ETS2Config.OwnershipStatus) + '",' + #13#10 +
    '      "fileHash": "' + EscapeJson(ETS2Config.ExistingFileHash) + '",' + #13#10 +
    '      "backupPath": "' + EscapeJson(ETS2Config.BackupPath) + '"' + #13#10 +
    '    },' + #13#10 +
    '    "ATS": {' + #13#10 +
    '      "configured": ' + LowerCase(BoolToStr(ATSConfig.UserSelected)) + ',' + #13#10 +
    '      "gamePath": "' + EscapeJson(ATSConfig.SelectedPath) + '",' + #13#10 +
    '      "pluginPath": "' + EscapeJson(ATSConfig.TargetPluginFile) + '",' + #13#10 +
    '      "status": "' + EscapeJson(ATSConfig.OwnershipStatus) + '",' + #13#10 +
    '      "fileHash": "' + EscapeJson(ATSConfig.ExistingFileHash) + '",' + #13#10 +
    '      "backupPath": "' + EscapeJson(ATSConfig.BackupPath) + '"' + #13#10 +
    '    }' + #13#10 +
    '  }' + #13#10 +
    '}' + #13#10;

  try
    Result := SaveStringToFile(StateFile, Json, False);
    if Result then
      LogInfo('Saved canonical v3 install state to: ' + StateFile)
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
  if not SafeDirExists(TargetDir) then
  begin
    StepIdx := BeginTransactionStep('CreateDir', '', TargetDir, '');
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

    // Check if this is a clean update of an existing Widget-owned plugin
    IsCleanOwnedUpdate := (Game.OwnershipStatus = OWNERSHIP_OWNED);

    if IsCleanOwnedUpdate then
    begin
      // CLEAN WIDGET-OWNED UPDATE:
      // Create temporary staging rollback backup, NOT persistent .trucksim_backup!
      StagingDir := GetTransactionStagingDir();
      if not SafeDirExists(StagingDir) then ForceDirectories(StagingDir);
      StagingBackupFile := CombinePath(StagingDir, Game.GameId + '_rollback_' + GetDateTimeString('yyyymmdd_hhnnss', '', '') + '.dll');

      LogInfo('[' + Game.GameId + '] Creating temporary staging backup of owned plugin at: ' + StagingBackupFile);
      StepIdx := BeginTransactionStep('StageOwnedBackup', TargetFile, TargetFile, StagingBackupFile);
      if not CopyFile(TargetFile, StagingBackupFile, False) then
      begin
        if not CopyFileElevated(TargetFile, StagingBackupFile) then
        begin
          LogErr('[' + Game.GameId + '] Failed to create temporary staging backup.');
          exit;
        end;
      end;
      CompleteTransactionStep(StepIdx);

      // Important: Persistent BackupPath remains empty!
      Game.BackupPath := '';
    end
    else
    begin
      // THIRD-PARTY OR UNVERIFIED CONFLICT REPLACEMENT:
      if (Game.ConflictAction = CONFLICT_ACTION_BACKUP_REPLACE) or
         (Game.OwnershipStatus = OWNERSHIP_LEGACY_UNVERIFIED) or
         (Game.OwnershipStatus = OWNERSHIP_MODIFIED_BY_USER) or
         (Game.OwnershipStatus = OWNERSHIP_NONE) then
      begin
        BackupTarget := CombinePath(TargetDir, PLUGIN_FILENAME + PLUGIN_BACKUP_EXT);
        if SafeFileExists(BackupTarget) then
          BackupTarget := CombinePath(TargetDir, PLUGIN_FILENAME + '.backup_' + GetDateTimeString('yyyymmdd_hhnnss', '', '') + '.bak');

        LogInfo('[' + Game.GameId + '] Creating persistent backup of third-party/unverified plugin: ' + BackupTarget);
        StepIdx := BeginTransactionStep('CreateThirdPartyBackup', TargetFile, TargetFile, BackupTarget);
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
      end;
    end;

    // Delete existing before copy
    if not DeleteFile(TargetFile) then
      DeleteFileElevated(TargetFile);
  end;

  // Copy new plugin file
  StepIdx := BeginTransactionStep('CopyPlugin', SourcePluginFile, TargetFile, Game.BackupPath);
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

    if IsCleanOwnedUpdate then
      Game.OwnershipStatus := OWNERSHIP_OWNED
    else if Game.BackupPath <> '' then
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
