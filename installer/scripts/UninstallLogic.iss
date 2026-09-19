// UninstallLogic.iss - Smart Non-Destructive Uninstall & Plugin Restoration
// Part of TruckSim Widget Installer

[Code]
procedure ProcessGamePluginUninstall(const GameId: String; const TargetPluginFile, RecordedHash, OwnershipState, BackupPath: String);
var
  CurrentHash: String;
begin
  if not SafeFileExists(TargetPluginFile) then
  begin
    LogInfo('[' + GameId + '] Plugin file does not exist at uninstall time: ' + TargetPluginFile);
    exit;
  end;

  CurrentHash := GetFileSha256Safe(TargetPluginFile);
  LogInfo('[' + GameId + '] Checking plugin during uninstall. Current hash: ' + CurrentHash + ', Recorded: ' + RecordedHash);

  // Check if modified by user
  if (RecordedHash <> '') and (CompareText(CurrentHash, RecordedHash) <> 0) then
  begin
    LogWarn('[' + GameId + '] Plugin file was modified after installation! Preserving file without deletion.');
    exit;
  end;

  // Case: Third-party plugin was backed up and replaced
  if (OwnershipState = OWNERSHIP_THIRD_PARTY_REPLACED) and SafeFileExists(BackupPath) then
  begin
    LogInfo('[' + GameId + '] Restoring original third-party plugin from: ' + BackupPath);
    try
      if CopyFile(BackupPath, TargetPluginFile, False) then
      begin
        DeleteFile(BackupPath);
        LogInfo('[' + GameId + '] Successfully restored original plugin.');
      end
      else
        LogErr('[' + GameId + '] Failed to restore original plugin from backup.');
    except
      LogErr('[' + GameId + '] Exception while restoring plugin backup.');
    end;
    exit;
  end;

  // Case: Clean Widget-owned plugin
  if (OwnershipState = OWNERSHIP_OWNED) or (OwnershipState = OWNERSHIP_LEGACY_UNVERIFIED) then
  begin
    LogInfo('[' + GameId + '] Removing Widget-owned plugin: ' + TargetPluginFile);
    try
      if DeleteFile(TargetPluginFile) then
        LogInfo('[' + GameId + '] Plugin successfully removed.')
      else
        LogWarn('[' + GameId + '] Could not delete plugin file (may be locked).');
    except
      LogErr('[' + GameId + '] Exception while deleting plugin.');
    end;
  end;
end;

procedure ExecuteUninstallCleanup();
var
  RemoveUserData: Boolean;
  PromptMsg: String;
  ETS2Config, ATSConfig: TGameConfig;
  WidgetAppData: String;
begin
  LogInfo('=== TRUCKSIM WIDGET UNINSTALL STARTED ===');

  // 1. Ask user about user data
  PromptMsg := CustomMessage('UninstallPromptUserData');
  RemoveUserData := MsgBox(PromptMsg, mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;

  if RemoveUserData then
    LogInfo('User chose to DELETE user data.')
  else
    LogInfo('User chose to PRESERVE user data.');

  // 2. Read install-state.json to get plugin details
  if ReadJsonStateFile(ETS2Config, ATSConfig) then
  begin
    if ETS2Config.SelectedPath <> '' then
      ProcessGamePluginUninstall('ETS2',
        CombinePath(CombinePath(ETS2Config.SelectedPath, GAME_REL_PLUGIN_DIR), PLUGIN_FILENAME),
        ETS2Config.ExistingFileHash, ETS2Config.OwnershipStatus, ETS2Config.BackupPath);

    if ATSConfig.SelectedPath <> '' then
      ProcessGamePluginUninstall('ATS',
        CombinePath(CombinePath(ATSConfig.SelectedPath, GAME_REL_PLUGIN_DIR), PLUGIN_FILENAME),
        ATSConfig.ExistingFileHash, ATSConfig.OwnershipStatus, ATSConfig.BackupPath);
  end;

  // 3. User data cleanup
  WidgetAppData := GetAppDataWidgetDir();
  if RemoveUserData then
  begin
    LogInfo('Removing user data folder: ' + WidgetAppData);
    if SafeDirExists(WidgetAppData) then
      DelTree(WidgetAppData, True, True, True);

    // Clean up Registry
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\TruckSim Widget');
    LogInfo('Registry keys removed.');
  end
  else
  begin
    LogInfo('User data preserved at: ' + WidgetAppData);
    // Keep registry keys or only remove installer transients if needed
  end;

  LogInfo('=== TRUCKSIM WIDGET UNINSTALL COMPLETED ===');
end;
