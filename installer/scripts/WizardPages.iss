// WizardPages.iss - Modern Dynamic Wizard Flow & TruckSim Widget Styling
// Part of TruckSim Widget Installer

[Code]
var
  PluginOptionsPage: TInputOptionWizardPage;
  GameConfigPage: TWizardPage;
  ConflictPage: TWizardPage;
  
  // Game config page controls
  ETS2PathEdit: TNewEdit;
  ETS2BrowseBtn: TNewButton;
  ETS2StatusLabel: TNewStaticText;
  ETS2PathLabel: TNewStaticText;

  ATSPathEdit: TNewEdit;
  ATSBrowseBtn: TNewButton;
  ATSStatusLabel: TNewStaticText;
  ATSPathLabel: TNewStaticText;

  // Conflict page controls
  ConflictHeaderLabel: TNewStaticText;
  ConflictDescLabel: TNewStaticText;
  ConflictOptionBackup: TNewRadioButton;
  ConflictOptionKeep: TNewRadioButton;
  ConflictOptionOverwrite: TNewRadioButton;

  // State
  GlobalETS2Config: TGameConfig;
  GlobalATSConfig: TGameConfig;
  HasConflict: Boolean;
  InitialUpdateDir: String;

procedure ApplyWidgetTheme();
begin
  WizardForm.Font.Name := 'Segoe UI';
  // Standard modern wizard styling
end;

procedure BrowseETS2FolderClick(Sender: TObject);
var
  Dir: String;
begin
  Dir := ETS2PathEdit.Text;
  if BrowseForFolder(CustomMessage('BrowseETS2Title'), Dir, False) then
    ETS2PathEdit.Text := Dir;
end;

procedure BrowseATSFolderClick(Sender: TObject);
var
  Dir: String;
begin
  Dir := ATSPathEdit.Text;
  if BrowseForFolder(CustomMessage('BrowseATSTitle'), Dir, False) then
    ATSPathEdit.Text := Dir;
end;

procedure CreateGameConfigControls();
var
  TopPos: Integer;
begin
  GameConfigPage := CreateCustomPage(
    PluginOptionsPage.ID,
    CustomMessage('GameDirPageTitle'),
    CustomMessage('GameDirPageSub')
  );

  TopPos := ScaleY(10);

  // ETS2 Controls
  ETS2PathLabel := TNewStaticText.Create(WizardForm);
  ETS2PathLabel.Parent := GameConfigPage.Surface;
  ETS2PathLabel.Caption := CustomMessage('ETS2DirPrompt');
  ETS2PathLabel.Top := TopPos;
  ETS2PathLabel.Left := ScaleX(5);
  ETS2PathLabel.Font.Style := [fsBold];

  TopPos := TopPos + ScaleY(22);
  ETS2PathEdit := TNewEdit.Create(WizardForm);
  ETS2PathEdit.Parent := GameConfigPage.Surface;
  ETS2PathEdit.Left := ScaleX(5);
  ETS2PathEdit.Top := TopPos;
  ETS2PathEdit.Width := ScaleX(320);

  ETS2BrowseBtn := TNewButton.Create(WizardForm);
  ETS2BrowseBtn.Parent := GameConfigPage.Surface;
  ETS2BrowseBtn.Left := ScaleX(335);
  ETS2BrowseBtn.Top := TopPos - ScaleY(1);
  ETS2BrowseBtn.Width := ScaleX(75);
  ETS2BrowseBtn.Height := ScaleY(25);
  ETS2BrowseBtn.Caption := CustomMessage('BrowseBtn');
  ETS2BrowseBtn.OnClick := @BrowseETS2FolderClick;

  TopPos := TopPos + ScaleY(28);
  ETS2StatusLabel := TNewStaticText.Create(WizardForm);
  ETS2StatusLabel.Parent := GameConfigPage.Surface;
  ETS2StatusLabel.Left := ScaleX(5);
  ETS2StatusLabel.Top := TopPos;
  ETS2StatusLabel.Caption := '';

  TopPos := TopPos + ScaleY(35);

  // ATS Controls
  ATSPathLabel := TNewStaticText.Create(WizardForm);
  ATSPathLabel.Parent := GameConfigPage.Surface;
  ATSPathLabel.Caption := CustomMessage('ATSDirPrompt');
  ATSPathLabel.Top := TopPos;
  ATSPathLabel.Left := ScaleX(5);
  ATSPathLabel.Font.Style := [fsBold];

  TopPos := TopPos + ScaleY(22);
  ATSPathEdit := TNewEdit.Create(WizardForm);
  ATSPathEdit.Parent := GameConfigPage.Surface;
  ATSPathEdit.Left := ScaleX(5);
  ATSPathEdit.Top := TopPos;
  ATSPathEdit.Width := ScaleX(320);

  ATSBrowseBtn := TNewButton.Create(WizardForm);
  ATSBrowseBtn.Parent := GameConfigPage.Surface;
  ATSBrowseBtn.Left := ScaleX(335);
  ATSBrowseBtn.Top := TopPos - ScaleY(1);
  ATSBrowseBtn.Width := ScaleX(75);
  ATSBrowseBtn.Height := ScaleY(25);
  ATSBrowseBtn.Caption := CustomMessage('BrowseBtn');
  ATSBrowseBtn.OnClick := @BrowseATSFolderClick;

  TopPos := TopPos + ScaleY(28);
  ATSStatusLabel := TNewStaticText.Create(WizardForm);
  ATSStatusLabel.Parent := GameConfigPage.Surface;
  ATSStatusLabel.Left := ScaleX(5);
  ATSStatusLabel.Top := TopPos;
  ATSStatusLabel.Caption := '';
end;

procedure CreateConflictResolutionControls();
var
  TopPos: Integer;
begin
  ConflictPage := CreateCustomPage(
    GameConfigPage.ID,
    CustomMessage('ConflictPageTitle'),
    CustomMessage('ConflictPageSub')
  );

  TopPos := ScaleY(10);
  ConflictHeaderLabel := TNewStaticText.Create(WizardForm);
  ConflictHeaderLabel.Parent := ConflictPage.Surface;
  ConflictHeaderLabel.Top := TopPos;
  ConflictHeaderLabel.Left := ScaleX(5);
  ConflictHeaderLabel.Width := ScaleX(400);
  ConflictHeaderLabel.Font.Style := [fsBold];
  ConflictHeaderLabel.Caption := CustomMessage('ConflictHeader');

  TopPos := TopPos + ScaleY(28);
  ConflictDescLabel := TNewStaticText.Create(WizardForm);
  ConflictDescLabel.Parent := ConflictPage.Surface;
  ConflictDescLabel.Top := TopPos;
  ConflictDescLabel.Left := ScaleX(5);
  ConflictDescLabel.Width := ScaleX(400);
  ConflictDescLabel.Caption := CustomMessage('ConflictDesc');

  TopPos := TopPos + ScaleY(55);
  ConflictOptionBackup := TNewRadioButton.Create(WizardForm);
  ConflictOptionBackup.Parent := ConflictPage.Surface;
  ConflictOptionBackup.Top := TopPos;
  ConflictOptionBackup.Left := ScaleX(15);
  ConflictOptionBackup.Width := ScaleX(390);
  ConflictOptionBackup.Caption := CustomMessage('ConflictOptionBackup');
  ConflictOptionBackup.Checked := True;

  TopPos := TopPos + ScaleY(35);
  ConflictOptionKeep := TNewRadioButton.Create(WizardForm);
  ConflictOptionKeep.Parent := ConflictPage.Surface;
  ConflictOptionKeep.Top := TopPos;
  ConflictOptionKeep.Left := ScaleX(15);
  ConflictOptionKeep.Width := ScaleX(390);
  ConflictOptionKeep.Caption := CustomMessage('ConflictOptionKeep');

  TopPos := TopPos + ScaleY(35);
  ConflictOptionOverwrite := TNewRadioButton.Create(WizardForm);
  ConflictOptionOverwrite.Parent := ConflictPage.Surface;
  ConflictOptionOverwrite.Top := TopPos;
  ConflictOptionOverwrite.Left := ScaleX(15);
  ConflictOptionOverwrite.Width := ScaleX(390);
  ConflictOptionOverwrite.Caption := CustomMessage('ConflictOptionOverwrite');
end;

function ValidateGameWithRunningCheck(var Game: TGameConfig; const PathEditVal: String): Boolean;
var
  Reason: String;
  DlgResult: Integer;
  Msg: String;
begin
  Result := False;
  Game.SelectedPath := NormalizePath(PathEditVal);

  // 1. Structure validation
  if not ValidateGameFolder(Game.SelectedPath, Game.ExeName, Reason) then
  begin
    MsgBox(Reason, mbError, MB_OK);
    exit;
  end;

  // 2. Running process check
  while IsGameProcessRunning(Game.ExeName) do
  begin
    Msg := FmtMessage(CustomMessage('ErrGameRunningPrompt'), [Game.DisplayName, Game.ExeName]);
    DlgResult := MsgBox(Msg, mbError, MB_ABORTRETRYIGNORE);
    if DlgResult = IDRETRY then
    begin
      // Loop again
    end
    else if DlgResult = IDIGNORE then
    begin
      LogWarn('User chose to ignore running game warning for: ' + Game.DisplayName);
      Break;
    end
    else
    begin
      // Abort
      LogInfo('User aborted setup due to running game: ' + Game.DisplayName);
      exit;
    end;
  end;

  // 3. Write permission probe
  if not IsDirectoryWritable(CombinePath(Game.SelectedPath, GAME_REL_BIN_DIR)) then
  begin
    LogWarn('[' + Game.GameId + '] Game directory is not directly writable. Will require elevation.');
    Game.NeedsElevation := True;
  end
  else
    Game.NeedsElevation := False;

  Result := True;
end;

function WizardShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;

  // Skip Game Config if no games selected
  if PageID = GameConfigPage.ID then
  begin
    Result := (not PluginOptionsPage.Values[0]) and (not PluginOptionsPage.Values[1]);
    exit;
  end;

  // Skip Conflict Page if no conflict exists
  if PageID = ConflictPage.ID then
  begin
    Result := not HasConflict;
    exit;
  end;

  // In fast update mode, skip select dir if already configured
  if IsUpdateMode() and (PageID = wpSelectDir) then
  begin
    if (InitialUpdateDir <> '') and SafeDirExists(InitialUpdateDir) then
      Result := True;
    exit;
  end;
end;

procedure UpdateGameConfigPageVisibility();
begin
  ETS2PathLabel.Visible := PluginOptionsPage.Values[0];
  ETS2PathEdit.Visible := PluginOptionsPage.Values[0];
  ETS2BrowseBtn.Visible := PluginOptionsPage.Values[0];
  ETS2StatusLabel.Visible := PluginOptionsPage.Values[0];

  ATSPathLabel.Visible := PluginOptionsPage.Values[1];
  ATSPathEdit.Visible := PluginOptionsPage.Values[1];
  ATSBrowseBtn.Visible := PluginOptionsPage.Values[1];
  ATSStatusLabel.Visible := PluginOptionsPage.Values[1];
end;

function HandleNextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  // Directory selection validation
  if CurPageID = wpSelectDir then
  begin
    if not IsDirectoryWritable(WizardDirValue()) then
    begin
      MsgBox(CustomMessage('ErrTargetDirNotWritable'), mbError, MB_OK);
      Result := False;
      exit;
    end;
  end;

  // Plugin options selection
  if CurPageID = PluginOptionsPage.ID then
  begin
    GlobalETS2Config.UserSelected := PluginOptionsPage.Values[0];
    GlobalATSConfig.UserSelected := PluginOptionsPage.Values[1];
    UpdateGameConfigPageVisibility();
  end;

  // Game config validation
  if CurPageID = GameConfigPage.ID then
  begin
    if GlobalETS2Config.UserSelected then
    begin
      if not ValidateGameWithRunningCheck(GlobalETS2Config, ETS2PathEdit.Text) then
      begin
        Result := False;
        exit;
      end;
      EvaluatePluginOwnership(GlobalETS2Config, BundledPluginHash);
    end;

    if GlobalATSConfig.UserSelected then
    begin
      if not ValidateGameWithRunningCheck(GlobalATSConfig, ATSPathEdit.Text) then
      begin
        Result := False;
        exit;
      end;
      EvaluatePluginOwnership(GlobalATSConfig, BundledPluginHash);
    end;

    // Check if any game has conflict
    HasConflict := False;
    if GlobalETS2Config.UserSelected and (GlobalETS2Config.ExistingFileFound) and
       (GlobalETS2Config.OwnershipStatus <> OWNERSHIP_OWNED) then
      HasConflict := True;

    if GlobalATSConfig.UserSelected and (GlobalATSConfig.ExistingFileFound) and
       (GlobalATSConfig.OwnershipStatus <> OWNERSHIP_OWNED) then
      HasConflict := True;

    LogInfo('Conflict evaluation result: HasConflict=' + BoolToStr(HasConflict));
  end;

  // Conflict resolution selection
  if CurPageID = ConflictPage.ID then
  begin
    if ConflictOptionBackup.Checked then
    begin
      GlobalETS2Config.ConflictAction := CONFLICT_ACTION_BACKUP_REPLACE;
      GlobalATSConfig.ConflictAction := CONFLICT_ACTION_BACKUP_REPLACE;
    end
    else if ConflictOptionKeep.Checked then
    begin
      GlobalETS2Config.ConflictAction := CONFLICT_ACTION_KEEP_EXISTING;
      GlobalATSConfig.ConflictAction := CONFLICT_ACTION_KEEP_EXISTING;
    end
    else
    begin
      GlobalETS2Config.ConflictAction := CONFLICT_ACTION_OVERWRITE;
      GlobalATSConfig.ConflictAction := CONFLICT_ACTION_OVERWRITE;
    end;
  end;
end;

function BuildReadySummary(): String;
var
  S: String;
begin
  S := CustomMessage('SummaryDestDir') + #13#10 + '  ' + WizardDirValue() + #13#10#13#10;
  
  S := S + CustomMessage('SummaryPlugins') + #13#10;
  if GlobalETS2Config.UserSelected then
    S := S + '  ETS2: ' + GlobalETS2Config.SelectedPath + #13#10
  else
    S := S + '  ETS2: ' + CustomMessage('SummarySkipped') + #13#10;

  if GlobalATSConfig.UserSelected then
    S := S + '  ATS: ' + GlobalATSConfig.SelectedPath + #13#10
  else
    S := S + '  ATS: ' + CustomMessage('SummarySkipped') + #13#10;

  if HasConflict then
  begin
    S := S + #13#10 + CustomMessage('SummaryConflictAction') + #13#10;
    if ConflictOptionBackup.Checked then
      S := S + '  ' + CustomMessage('ConflictOptionBackup') + #13#10
    else if ConflictOptionKeep.Checked then
      S := S + '  ' + CustomMessage('ConflictOptionKeep') + #13#10
    else
      S := S + '  ' + CustomMessage('ConflictOptionOverwrite') + #13#10;
  end;

  Result := S;
end;
