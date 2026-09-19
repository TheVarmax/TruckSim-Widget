// TransactionJournal.iss - On-Disk Persistent Transaction Journal & Crash Recovery
// Part of TruckSim Widget Installer

[Code]
type
  TJournalStep = record
    StepIndex: Integer;
    Operation: String;    // 'StageRollbackBackup', 'CreateThirdPartyBackup', 'CopyPlugin', 'CreateDir'
    SourcePath: String;
    TargetPath: String;
    BackupPath: String;
    OriginalHash: String; // SHA-256 of target prior to operation
    Status: String;       // 'STEP_PENDING', 'STEP_COMPLETED'
  end;

var
  JournalSteps: array of TJournalStep;
  JournalTxId: String;
  JournalStatus: String;

procedure FlushJournalToDisk();
var
  JournalPath: String;
  JournalDir: String;
  Json: String;
  I: Integer;
  NowIso: String;
begin
  JournalPath := GetTransactionJournalFilePath();
  JournalDir := ExtractFilePath(JournalPath);

  if not SafeDirExists(JournalDir) then
    ForceDirectories(JournalDir);

  NowIso := GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':');

  Json := '{' + #13#10 +
    '  "transactionId": "' + EscapeJson(JournalTxId) + '",' + #13#10 +
    '  "status": "' + EscapeJson(JournalStatus) + '",' + #13#10 +
    '  "timestamp": "' + EscapeJson(NowIso) + '",' + #13#10 +
    '  "steps": [' + #13#10;

  for I := 0 to GetArrayLength(JournalSteps) - 1 do
  begin
    Json := Json + '    {' + #13#10 +
      '      "stepIndex": ' + IntToStr(JournalSteps[I].StepIndex) + ',' + #13#10 +
      '      "operation": "' + EscapeJson(JournalSteps[I].Operation) + '",' + #13#10 +
      '      "sourcePath": "' + EscapeJson(JournalSteps[I].SourcePath) + '",' + #13#10 +
      '      "targetPath": "' + EscapeJson(JournalSteps[I].TargetPath) + '",' + #13#10 +
      '      "backupPath": "' + EscapeJson(JournalSteps[I].BackupPath) + '",' + #13#10 +
      '      "originalHash": "' + EscapeJson(JournalSteps[I].OriginalHash) + '",' + #13#10 +
      '      "status": "' + EscapeJson(JournalSteps[I].Status) + '"' + #13#10 +
      '    }';
    if I < GetArrayLength(JournalSteps) - 1 then
      Json := Json + ',';
    Json := Json + #13#10;
  end;

  Json := Json + '  ]' + #13#10 + '}' + #13#10;

  if not AtomicSaveStringToFile(JournalPath, Json) then
    LogErr('Failed to atomically flush transaction journal to disk: ' + JournalPath);
end;

procedure InitTransactionJournal();
begin
  SetArrayLength(JournalSteps, 0);
  JournalTxId := 'tx_' + GetDateTimeString('yyyymmdd_hhnnss', '_', '_');
  JournalStatus := TRANSACTION_STATUS_PENDING;
  LogInfo('Initialized transaction journal: ' + JournalTxId);
  FlushJournalToDisk();
end;

function BeginTransactionStep(const Op, Src, Target, Backup, OrigHash: String): Integer;
var
  Idx: Integer;
begin
  Idx := GetArrayLength(JournalSteps);
  SetArrayLength(JournalSteps, Idx + 1);

  JournalSteps[Idx].StepIndex := Idx;
  JournalSteps[Idx].Operation := Op;
  JournalSteps[Idx].SourcePath := Src;
  JournalSteps[Idx].TargetPath := Target;
  JournalSteps[Idx].BackupPath := Backup;
  JournalSteps[Idx].OriginalHash := OrigHash;
  JournalSteps[Idx].Status := STEP_STATUS_PENDING;

  FlushJournalToDisk();
  Result := Idx;
end;

procedure CompleteTransactionStep(const StepIdx: Integer);
begin
  if (StepIdx >= 0) and (StepIdx < GetArrayLength(JournalSteps)) then
  begin
    JournalSteps[StepIdx].Status := STEP_STATUS_COMPLETED;
    FlushJournalToDisk();
  end;
end;

function ExecuteStepRollback(const Step: TJournalStep): Boolean;
var
  RestoredHash: String;
  Copied: Boolean;
  Deleted: Boolean;
begin
  Result := True;
  LogInfo('Executing rollback for step ' + IntToStr(Step.StepIndex) + ' (' + Step.Operation + ')');

  if (Step.Operation = 'CopyPlugin') or (Step.Operation = 'StageRollbackBackup') then
  begin
    // 1. If TargetPath exists and was modified or created by CopyPlugin, delete it
    if Step.Operation = 'CopyPlugin' then
    begin
      if SafeFileExists(Step.TargetPath) then
      begin
        Deleted := DeleteFile(Step.TargetPath);
        if not Deleted then
          Deleted := DeleteFileElevated(Step.TargetPath);
        if not Deleted then
          LogWarn('Could not delete target file during rollback: ' + Step.TargetPath);
      end;
    end;

    // 2. Restore BackupPath -> TargetPath
    if (Step.BackupPath <> '') and SafeFileExists(Step.BackupPath) then
    begin
      LogInfo('Restoring original file from backup: ' + Step.BackupPath + ' -> ' + Step.TargetPath);
      Copied := CopyFile(Step.BackupPath, Step.TargetPath, False);
      if not Copied then
        Copied := CopyFileElevated(Step.BackupPath, Step.TargetPath);

      if not Copied or not SafeFileExists(Step.TargetPath) then
      begin
        LogErr('CRITICAL: Failed to restore file from rollback backup: ' + Step.BackupPath);
        Result := False;
        exit;
      end;

      // 3. Verify restored file SHA-256 against recorded OriginalHash
      if Step.OriginalHash <> '' then
      begin
        RestoredHash := GetFileSha256Safe(Step.TargetPath);
        if CompareText(RestoredHash, Step.OriginalHash) <> 0 then
        begin
          LogErr('CRITICAL: Restored file SHA-256 mismatch! Expected: ' + Step.OriginalHash + ', Actual: ' + RestoredHash);
          Result := False;
          exit;
        end;
        LogInfo('Restored file SHA-256 integrity verified: ' + RestoredHash);
      end;
    end;
  end
  else if Step.Operation = 'CreateThirdPartyBackup' then
  begin
    // If a persistent backup was created during this aborted transaction, clean it up or restore
    if (Step.BackupPath <> '') and SafeFileExists(Step.BackupPath) then
    begin
      // 1. If TargetPath does not exist, restore it from BackupPath
      if not SafeFileExists(Step.TargetPath) then
      begin
        LogInfo('Restoring target file from persistent backup: ' + Step.BackupPath + ' -> ' + Step.TargetPath);
        Copied := CopyFile(Step.BackupPath, Step.TargetPath, False);
        if not Copied then
          Copied := CopyFileElevated(Step.BackupPath, Step.TargetPath);
        if not Copied or not SafeFileExists(Step.TargetPath) then
        begin
          LogErr('CRITICAL: Failed to restore target file from persistent backup: ' + Step.BackupPath + '. Preserving backup.');
          Result := False;
          exit;
        end;
      end;

      // 2. TargetPath exists: verify its hash matches OriginalHash before deleting backup
      if SafeFileExists(Step.TargetPath) then
      begin
        if Step.OriginalHash <> '' then
        begin
          RestoredHash := GetFileSha256Safe(Step.TargetPath);
          if CompareText(RestoredHash, Step.OriginalHash) <> 0 then
          begin
            LogErr('CRITICAL: TargetPath hash (' + RestoredHash + ') does not match original hash (' + Step.OriginalHash + ')! Preserving persistent backup for diagnostics: ' + Step.BackupPath);
            Result := False;
            exit;
          end;
          LogInfo('Target file SHA-256 integrity verified against original backup hash: ' + RestoredHash);
        end;

        // Hash verified: safe to clean up redundant persistent backup
        if not DeleteFile(Step.BackupPath) then
        begin
          if not DeleteFileElevated(Step.BackupPath) then
            LogWarn('Could not delete redundant persistent backup: ' + Step.BackupPath);
        end;
      end
      else
      begin
        LogErr('CRITICAL: TargetPath does not exist after rollback attempt! Preserving backup: ' + Step.BackupPath);
        Result := False;
        exit;
      end;
    end;
  end
  else if Step.Operation = 'CreateDir' then
  begin
    if SafeDirExists(Step.TargetPath) then
    begin
      LogInfo('Removing directory created during transaction: ' + Step.TargetPath);
      if not RemoveDir(Step.TargetPath) then
      begin
        if not RmDirElevated(Step.TargetPath) then
          LogWarn('Could not remove created directory during rollback: ' + Step.TargetPath);
      end;
    end;
  end
  else if Step.Operation = 'SaveStateFile' then
  begin
    LogInfo('Rolling back install state file: ' + Step.TargetPath);
    if (Step.BackupPath <> '') and SafeFileExists(Step.BackupPath) then
    begin
      // Existing state was modified: restore from staging backup
      LogInfo('Restoring previous install state from staging backup: ' + Step.BackupPath + ' -> ' + Step.TargetPath);
      Copied := CopyFile(Step.BackupPath, Step.TargetPath, False);
      if not Copied then
        Copied := CopyFileElevated(Step.BackupPath, Step.TargetPath);

      if not Copied or not SafeFileExists(Step.TargetPath) then
      begin
        LogErr('CRITICAL: Failed to restore install state from backup: ' + Step.BackupPath);
        Result := False;
        exit;
      end;

      // Verify restored state file SHA-256 against recorded OriginalHash
      if Step.OriginalHash <> '' then
      begin
        RestoredHash := GetFileSha256Safe(Step.TargetPath);
        if CompareText(RestoredHash, Step.OriginalHash) <> 0 then
        begin
          LogErr('CRITICAL: Restored install state SHA-256 mismatch! Expected: ' + Step.OriginalHash + ', Actual: ' + RestoredHash);
          Result := False;
          exit;
        end;
        LogInfo('Restored install state SHA-256 verified successfully: ' + RestoredHash);
      end;
    end
    else
    begin
      // No previous state existed prior to this transaction: delete the newly created install-state.json
      LogInfo('No previous install state existed prior to transaction; removing created file: ' + Step.TargetPath);
      if SafeFileExists(Step.TargetPath) then
      begin
        Deleted := DeleteFile(Step.TargetPath);
        if not Deleted then
          Deleted := DeleteFileElevated(Step.TargetPath);

        if SafeFileExists(Step.TargetPath) then
        begin
          LogErr('CRITICAL: Failed to delete newly created install state file during rollback: ' + Step.TargetPath);
          Result := False;
          exit;
        end;
        LogInfo('Successfully removed newly created install state file during rollback.');
      end;
    end;
  end;
end;

procedure RollbackTransaction();
var
  I: Integer;
  AllSucceeded: Boolean;
begin
  LogWarn('Executing in-session rollback for: ' + JournalTxId);
  AllSucceeded := True;

  for I := GetArrayLength(JournalSteps) - 1 downto 0 do
  begin
    if JournalSteps[I].Status = STEP_STATUS_COMPLETED then
    begin
      if not ExecuteStepRollback(JournalSteps[I]) then
      begin
        AllSucceeded := False;
        LogErr('Rollback step ' + IntToStr(I) + ' failed verification!');
      end;
    end;
  end;

  if AllSucceeded then
  begin
    JournalStatus := TRANSACTION_STATUS_ROLLED_BACK;
    FlushJournalToDisk();
    LogInfo('In-session rollback completed and verified successfully.');

    // Clean staging directory
    if SafeDirExists(GetTransactionStagingDir()) then
    begin
      try
        DelTree(GetTransactionStagingDir(), True, True, True);
      except
      end;
    end;
  end
  else
  begin
    LogErr('In-session rollback encountered errors! Journal status remains PENDING for diagnostics.');
  end;
end;

procedure CommitTransaction();
var
  JournalPath: String;
begin
  JournalStatus := TRANSACTION_STATUS_COMMITTED;
  FlushJournalToDisk();
  LogInfo('Transaction ' + JournalTxId + ' committed successfully.');

  // Clean staging files
  if SafeDirExists(GetTransactionStagingDir()) then
  begin
    try
      DelTree(GetTransactionStagingDir(), True, True, True);
    except
    end;
  end;

  // Remove committed journal
  JournalPath := GetTransactionJournalFilePath();
  if SafeFileExists(JournalPath) then
  begin
    try
      DeleteFile(JournalPath);
    except
    end;
  end;
end;

function ParseJournalSteps(const Json: String; var Steps: array of TJournalStep): Boolean;
var
  StepsPos, CurPos, OpenBrace, CloseBrace: Integer;
  Block: String;
  Idx: Integer;
begin
  Result := False;
  SetArrayLength(Steps, 0);

  StepsPos := Pos('"steps"', Json);
  if StepsPos <= 0 then exit;

  CurPos := StepsPos;
  while True do
  begin
    OpenBrace := Pos('{', Copy(Json, CurPos, Length(Json) - CurPos + 1));
    if OpenBrace <= 0 then Break;
    OpenBrace := CurPos + OpenBrace - 1;

    CloseBrace := Pos('}', Copy(Json, OpenBrace, Length(Json) - OpenBrace + 1));
    if CloseBrace <= 0 then Break;
    CloseBrace := OpenBrace + CloseBrace - 1;

    Block := Copy(Json, OpenBrace, CloseBrace - OpenBrace + 1);
    CurPos := CloseBrace + 1;

    Idx := GetArrayLength(Steps);
    SetArrayLength(Steps, Idx + 1);

    Steps[Idx].StepIndex := StrToIntDef(ExtractJsonValue(Block, 'stepIndex'), Idx);
    Steps[Idx].Operation := ExtractJsonValue(Block, 'operation');
    Steps[Idx].SourcePath := ExtractJsonValue(Block, 'sourcePath');
    Steps[Idx].TargetPath := ExtractJsonValue(Block, 'targetPath');
    Steps[Idx].BackupPath := ExtractJsonValue(Block, 'backupPath');
    Steps[Idx].OriginalHash := ExtractJsonValue(Block, 'originalHash');
    Steps[Idx].Status := ExtractJsonValue(Block, 'status');
  end;

  Result := (GetArrayLength(Steps) > 0);
end;

function CheckAndExecuteCrashRecovery(): Boolean;
var
  JournalPath: String;
  RawContent: AnsiString;
  ContentStr: String;
  StatusVal: String;
  Steps: array of TJournalStep;
  I: Integer;
  RecoveryOk: Boolean;
begin
  Result := False;
  JournalPath := GetTransactionJournalFilePath();

  // If no journal exists, clean up any orphaned staging files from past completed sessions
  if not SafeFileExists(JournalPath) then
  begin
    if SafeDirExists(GetTransactionStagingDir()) then
    begin
      try
        DelTree(GetTransactionStagingDir(), True, True, True);
      except
      end;
    end;
    exit;
  end;

  LogInfo('Found existing transaction journal at: ' + JournalPath);
  if not LoadStringFromFile(JournalPath, RawContent) then
  begin
    LogWarn('Could not read transaction journal for recovery check.');
    exit;
  end;

  ContentStr := String(RawContent);

  // Validate JSON integrity: must have transactionId, status, and steps
  if (Pos('"transactionId"', ContentStr) <= 0) or (Pos('"status"', ContentStr) <= 0) then
  begin
    LogErr('Transaction journal is corrupted or unparseable! Preserving journal and staging for diagnostics. Automatic recovery halted.');
    exit;
  end;

  StatusVal := ExtractJsonValue(ContentStr, 'status');

  if StatusVal = TRANSACTION_STATUS_PENDING then
  begin
    LogWarn('Detected incomplete PENDING transaction from prior crashed session! Executing automated crash recovery.');

    if not ParseJournalSteps(ContentStr, Steps) then
    begin
      LogErr('Could not parse transaction steps from journal. Preserving files for diagnostics.');
      exit;
    end;

    RecoveryOk := True;
    // Walk completed steps in reverse order
    for I := GetArrayLength(Steps) - 1 downto 0 do
    begin
      if Steps[I].Status = STEP_STATUS_COMPLETED then
      begin
        if not ExecuteStepRollback(Steps[I]) then
        begin
          RecoveryOk := False;
          LogErr('Crash recovery failed on step ' + IntToStr(I) + ' (' + Steps[I].Operation + ')');
        end;
      end;
    end;

    if RecoveryOk then
    begin
      LogInfo('Crash recovery completed and verified all steps successfully.');
      // Clean staging files
      if SafeDirExists(GetTransactionStagingDir()) then
      begin
        try
          DelTree(GetTransactionStagingDir(), True, True, True);
        except
        end;
      end;

      // Delete recovered journal
      DeleteFile(JournalPath);
      Result := True;
    end
    else
    begin
      LogErr('Crash recovery was unable to verify all steps. Preserving journal and staging for manual inspection.');
    end;
  end
  else if (StatusVal = TRANSACTION_STATUS_COMMITTED) or (StatusVal = TRANSACTION_STATUS_ROLLED_BACK) then
  begin
    // Stale completed or rolled back journal: safely clean staging and journal
    if SafeDirExists(GetTransactionStagingDir()) then
    begin
      try
        DelTree(GetTransactionStagingDir(), True, True, True);
      except
      end;
    end;
    DeleteFile(JournalPath);
  end;
end;
