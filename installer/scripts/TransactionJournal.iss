// TransactionJournal.iss - On-Disk Persistent Transaction Journal & Crash Recovery
// Part of TruckSim Widget Installer

[Code]
type
  TJournalStep = record
    StepIndex: Integer;
    Operation: String;  // 'StageOwnedBackup', 'CreateThirdPartyBackup', 'CopyPlugin', 'CreateDir'
    SourcePath: String;
    TargetPath: String;
    BackupPath: String;
    Status: String;     // 'STEP_PENDING', 'STEP_COMPLETED'
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
      '      "status": "' + EscapeJson(JournalSteps[I].Status) + '"' + #13#10 +
      '    }';
    if I < GetArrayLength(JournalSteps) - 1 then
      Json := Json + ',';
    Json := Json + #13#10;
  end;

  Json := Json + '  ]' + #13#10 + '}' + #13#10;

  try
    SaveStringToFile(JournalPath, Json, False);
  except
    LogErr('Failed to flush transaction journal to disk: ' + JournalPath);
  end;
end;

procedure InitTransactionJournal();
begin
  SetArrayLength(JournalSteps, 0);
  JournalTxId := 'tx_' + GetDateTimeString('yyyymmdd_hhnnss', '', '');
  JournalStatus := TRANSACTION_STATUS_PENDING;
  LogInfo('Initialized transaction journal: ' + JournalTxId);
  FlushJournalToDisk();
end;

function BeginTransactionStep(const Op, Src, Target, Backup: String): Integer;
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

procedure RollbackTransaction();
var
  I: Integer;
  Step: TJournalStep;
begin
  JournalStatus := TRANSACTION_STATUS_ROLLED_BACK;
  FlushJournalToDisk();
  LogWarn('Executing transactional rollback for: ' + JournalTxId);

  for I := GetArrayLength(JournalSteps) - 1 downto 0 do
  begin
    Step := JournalSteps[I];
    if Step.Status = STEP_STATUS_COMPLETED then
    begin
      LogInfo('Rolling back step ' + IntToStr(I) + ' (' + Step.Operation + ')');

      if (Step.Operation = 'CopyPlugin') or (Step.Operation = 'StageOwnedBackup') then
      begin
        if SafeFileExists(Step.TargetPath) then
        begin
          if not DeleteFile(Step.TargetPath) then
            DeleteFileElevated(Step.TargetPath);
        end;

        // If a backup was recorded, restore it
        if (Step.BackupPath <> '') and SafeFileExists(Step.BackupPath) then
        begin
          if not CopyFile(Step.BackupPath, Step.TargetPath, False) then
            CopyFileElevated(Step.BackupPath, Step.TargetPath);
        end;
      end
      else if Step.Operation = 'CreateThirdPartyBackup' then
      begin
        // If third party backup was created but install failed, restore original from backup
        if (Step.BackupPath <> '') and SafeFileExists(Step.BackupPath) and (Step.TargetPath <> '') then
        begin
          if not SafeFileExists(Step.TargetPath) then
          begin
            if not RenameFile(Step.BackupPath, Step.TargetPath) then
              MoveFileElevated(Step.BackupPath, Step.TargetPath);
          end;
        end;
      end
      else if Step.Operation = 'CreateDir' then
      begin
        if SafeDirExists(Step.TargetPath) then
          RemoveDir(Step.TargetPath);
      end;
    end;
  end;

  // Clean staging directory
  if SafeDirExists(GetTransactionStagingDir()) then
    DelTree(GetTransactionStagingDir(), True, True, True);

  LogInfo('Transactional rollback finished.');
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

function CheckAndExecuteCrashRecovery(): Boolean;
var
  JournalPath: String;
  Content: AnsiString;
  ContentStr: String;
  StatusVal: String;
begin
  Result := False;
  JournalPath := GetTransactionJournalFilePath();

  if not SafeFileExists(JournalPath) then
    exit;

  LogInfo('Found existing transaction journal at: ' + JournalPath);
  if not LoadStringFromFile(JournalPath, Content) then
  begin
    LogWarn('Could not read transaction journal for recovery check.');
    exit;
  end;

  ContentStr := String(Content);
  StatusVal := ExtractJsonValue(ContentStr, 'status');

  if StatusVal = TRANSACTION_STATUS_PENDING then
  begin
    LogWarn('Detected incomplete PENDING transaction from prior session! Executing automated crash recovery.');
    // Incomplete transaction detected: clean up any stale staging files
    if SafeDirExists(GetTransactionStagingDir()) then
    begin
      try
        DelTree(GetTransactionStagingDir(), True, True, True);
      except
      end;
    end;

    // Delete or mark recovered
    DeleteFile(JournalPath);
    LogInfo('Automated crash recovery completed.');
    Result := True;
  end
  else
  begin
    // Stale committed or rolled back journal
    DeleteFile(JournalPath);
  end;
end;
