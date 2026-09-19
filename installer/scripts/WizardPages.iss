// WizardPages.iss - Modern Dynamic Wizard Flow & TruckSim Widget Branded Styling
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

  ETS2ConflictHeader: TNewStaticText;
  ETS2ConflictPanel: TPanel;
  ETS2ConflictOptionBackup: TNewRadioButton;
  ETS2ConflictOptionKeep: TNewRadioButton;
  ETS2ConflictOptionOverwrite: TNewRadioButton;

  ATSConflictHeader: TNewStaticText;
  ATSConflictPanel: TPanel;
  ATSConflictOptionBackup: TNewRadioButton;
  ATSConflictOptionKeep: TNewRadioButton;
  ATSConflictOptionOverwrite: TNewRadioButton;

  // State
  GlobalETS2Config: TGameConfig;
  GlobalATSConfig: TGameConfig;
  HasConflict: Boolean;
  HasETS2Conflict: Boolean;
  HasATSConflict: Boolean;
  InitialUpdateDir: String;

procedure ApplyWidgetTheme();
begin
  // Set clean modern typography matching modern Inno Setup styling
  WizardForm.Font.Name := 'Segoe UI';
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

procedure InitGameConfigs();
begin
  GlobalETS2Config.GameId := GAME_ETS2;
  GlobalETS2Config.DisplayName := CustomMessage('GameNameETS2');
  GlobalETS2Config.ExeName := GAME_EXE_ETS2;
  GlobalETS2Config.DefaultFolderName := GAME_DIRNAME_ETS2;
  GlobalETS2Config.SteamAppId := STEAM_APPID_ETS2;
  GlobalETS2Config.UserSelected := False;
  GlobalETS2Config.DetectedPath := '';
  GlobalETS2Config.SelectedPath := '';
  GlobalETS2Config.ExistingFileFound := False;
  GlobalETS2Config.ExistingFileHash := '';
  GlobalETS2Config.OwnershipStatus := OWNERSHIP_NONE;
  GlobalETS2Config.ConflictAction := CONFLICT_ACTION_NONE;
  GlobalETS2Config.BackupPath := '';
  GlobalETS2Config.BackupHash := '';
  GlobalETS2Config.NeedsElevation := False;
  GlobalETS2Config.IsRunning := False;

  GlobalATSConfig.GameId := GAME_ATS;
  GlobalATSConfig.DisplayName := CustomMessage('GameNameATS');
  GlobalATSConfig.ExeName := GAME_EXE_ATS;
  GlobalATSConfig.DefaultFolderName := GAME_DIRNAME_ATS;
  GlobalATSConfig.SteamAppId := STEAM_APPID_ATS;
  GlobalATSConfig.UserSelected := False;
  GlobalATSConfig.DetectedPath := '';
  GlobalATSConfig.SelectedPath := '';
  GlobalATSConfig.ExistingFileFound := False;
  GlobalATSConfig.ExistingFileHash := '';
  GlobalATSConfig.OwnershipStatus := OWNERSHIP_NONE;
  GlobalATSConfig.ConflictAction := CONFLICT_ACTION_NONE;
  GlobalATSConfig.BackupPath := '';
  GlobalATSConfig.BackupHash := '';
  GlobalATSConfig.NeedsElevation := False;
  GlobalATSConfig.IsRunning := False;

  HasConflict := False;
  HasETS2Conflict := False;
  HasATSConflict := False;
end;

procedure CreateGameSelectionControls();
var
  TopPos: Integer;
begin
  // 1. Component Selection Page
  PluginOptionsPage := CreateInputOptionPage(
    wpSelectDir,
    CustomMessage('TelemetryPageTitle'),
    CustomMessage('TelemetryPageSub'),
    CustomMessage('TelemetryPagePrompt'),
    False,
    False
  );
  PluginOptionsPage.Add(CustomMessage('InstallETS2Plugin'));
  PluginOptionsPage.Add(CustomMessage('InstallATSPlugin'));

  // Pre-select if detected
  PluginOptionsPage.Values[0] := GlobalETS2Config.DetectedPath <> '';
  PluginOptionsPage.Values[1] := GlobalATSConfig.DetectedPath <> '';

  // 2. Game Directories Configuration Page
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

  TopPos := ScaleY(5);
  ConflictHeaderLabel := TNewStaticText.Create(WizardForm);
  ConflictHeaderLabel.Parent := ConflictPage.Surface;
  ConflictHeaderLabel.Top := TopPos;
  ConflictHeaderLabel.Left := ScaleX(5);
  ConflictHeaderLabel.Width := ScaleX(400);
  ConflictHeaderLabel.Font.Style := [fsBold];
  ConflictHeaderLabel.Caption := CustomMessage('ConflictHeader');

  TopPos := TopPos + ScaleY(20);
  ConflictDescLabel := TNewStaticText.Create(WizardForm);
  ConflictDescLabel.Parent := ConflictPage.Surface;
  ConflictDescLabel.Top := TopPos;
  ConflictDescLabel.Left := ScaleX(5);
  ConflictDescLabel.Width := ScaleX(400);
  ConflictDescLabel.Caption := CustomMessage('ConflictDesc');

  // ETS2 Conflict Section
  TopPos := TopPos + ScaleY(26);
  ETS2ConflictHeader := TNewStaticText.Create(WizardForm);
  ETS2ConflictHeader.Parent := ConflictPage.Surface;
  ETS2ConflictHeader.Top := TopPos;
  ETS2ConflictHeader.Left := ScaleX(5);
  ETS2ConflictHeader.Font.Style := [fsBold];
  ETS2ConflictHeader.Caption := CustomMessage('GameNameETS2') + ':';

  TopPos := TopPos + ScaleY(18);
  ETS2ConflictPanel := TPanel.Create(WizardForm);
  ETS2ConflictPanel.Parent := ConflictPage.Surface;
  ETS2ConflictPanel.Left := ScaleX(5);
  ETS2ConflictPanel.Top := TopPos;
  ETS2ConflictPanel.Width := ScaleX(420);
  ETS2ConflictPanel.Height := ScaleY(68);
  ETS2ConflictPanel.BevelOuter := bvNone;
  ETS2ConflictPanel.ParentBackground := True;

  ETS2ConflictOptionBackup := TNewRadioButton.Create(WizardForm);
  ETS2ConflictOptionBackup.Parent := ETS2ConflictPanel;
  ETS2ConflictOptionBackup.Top := ScaleY(0);
  ETS2ConflictOptionBackup.Left := ScaleX(10);
  ETS2ConflictOptionBackup.Width := ScaleX(400);
  ETS2ConflictOptionBackup.Caption := CustomMessage('ConflictOptionBackup');
  ETS2ConflictOptionBackup.Checked := True;

  ETS2ConflictOptionKeep := TNewRadioButton.Create(WizardForm);
  ETS2ConflictOptionKeep.Parent := ETS2ConflictPanel;
  ETS2ConflictOptionKeep.Top := ScaleY(22);
  ETS2ConflictOptionKeep.Left := ScaleX(10);
  ETS2ConflictOptionKeep.Width := ScaleX(400);
  ETS2ConflictOptionKeep.Caption := CustomMessage('ConflictOptionKeep');

  ETS2ConflictOptionOverwrite := TNewRadioButton.Create(WizardForm);
  ETS2ConflictOptionOverwrite.Parent := ETS2ConflictPanel;
  ETS2ConflictOptionOverwrite.Top := ScaleY(44);
  ETS2ConflictOptionOverwrite.Left := ScaleX(10);
  ETS2ConflictOptionOverwrite.Width := ScaleX(400);
  ETS2ConflictOptionOverwrite.Caption := CustomMessage('ConflictOptionOverwrite');

  // ATS Conflict Section
  TopPos := TopPos + ScaleY(74);
  ATSConflictHeader := TNewStaticText.Create(WizardForm);
  ATSConflictHeader.Parent := ConflictPage.Surface;
  ATSConflictHeader.Top := TopPos;
  ATSConflictHeader.Left := ScaleX(5);
  ATSConflictHeader.Font.Style := [fsBold];
  ATSConflictHeader.Caption := CustomMessage('GameNameATS') + ':';

  TopPos := TopPos + ScaleY(18);
  ATSConflictPanel := TPanel.Create(WizardForm);
  ATSConflictPanel.Parent := ConflictPage.Surface;
  ATSConflictPanel.Left := ScaleX(5);
  ATSConflictPanel.Top := TopPos;
  ATSConflictPanel.Width := ScaleX(420);
  ATSConflictPanel.Height := ScaleY(68);
  ATSConflictPanel.BevelOuter := bvNone;
  ATSConflictPanel.ParentBackground := True;

  ATSConflictOptionBackup := TNewRadioButton.Create(WizardForm);
  ATSConflictOptionBackup.Parent := ATSConflictPanel;
  ATSConflictOptionBackup.Top := ScaleY(0);
  ATSConflictOptionBackup.Left := ScaleX(10);
  ATSConflictOptionBackup.Width := ScaleX(400);
  ATSConflictOptionBackup.Caption := CustomMessage('ConflictOptionBackup');
  ATSConflictOptionBackup.Checked := True;

  ATSConflictOptionKeep := TNewRadioButton.Create(WizardForm);
  ATSConflictOptionKeep.Parent := ATSConflictPanel;
  ATSConflictOptionKeep.Top := ScaleY(22);
  ATSConflictOptionKeep.Left := ScaleX(10);
  ATSConflictOptionKeep.Width := ScaleX(400);
  ATSConflictOptionKeep.Caption := CustomMessage('ConflictOptionKeep');

  ATSConflictOptionOverwrite := TNewRadioButton.Create(WizardForm);
  ATSConflictOptionOverwrite.Parent := ATSConflictPanel;
  ATSConflictOptionOverwrite.Top := ScaleY(44);
  ATSConflictOptionOverwrite.Left := ScaleX(10);
  ATSConflictOptionOverwrite.Width := ScaleX(400);
  ATSConflictOptionOverwrite.Caption := CustomMessage('ConflictOptionOverwrite');
end;

procedure UpdateConflictPageVisibility();
var
  CurTop: Integer;
begin
  CurTop := ScaleY(55);

  ETS2ConflictHeader.Visible := HasETS2Conflict;
  ETS2ConflictPanel.Visible := HasETS2Conflict;

  if HasETS2Conflict then
  begin
    ETS2ConflictHeader.Top := CurTop;
    CurTop := CurTop + ScaleY(18);
    ETS2ConflictPanel.Top := CurTop;
    CurTop := CurTop + ScaleY(72);
  end;

  ATSConflictHeader.Visible := HasATSConflict;
  ATSConflictPanel.Visible := HasATSConflict;

  if HasATSConflict then
  begin
    ATSConflictHeader.Top := CurTop;
    CurTop := CurTop + ScaleY(18);
    ATSConflictPanel.Top := CurTop;
  end;
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

  // 2. Running process check (Retry / Skip this game / Cancel)
  while IsGameProcessRunning(Game.ExeName) do
  begin
    Msg := FmtMessage(CustomMessage('ErrGameRunningPrompt'), [Game.DisplayName, Game.ExeName]);
    DlgResult := MsgBox(Msg, mbConfirmation, MB_YESNOCANCEL);
    if DlgResult = IDYES then
    begin
      // User closed game, retry check loop
    end
    else if DlgResult = IDNO then
    begin
      // Skip this game
      LogInfo('User chose to skip plugin installation for: ' + Game.DisplayName);
      Game.UserSelected := False;
      Result := True;
      exit;
    end
    else
    begin
      // Cancel setup
      LogInfo('User cancelled setup due to running game: ' + Game.DisplayName);
      Result := False;
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

  // Skip Conflict Page if no game has conflict
  if PageID = ConflictPage.ID then
  begin
    Result := (not HasETS2Conflict) and (not HasATSConflict);
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

    // Per-game conflict evaluation
    HasETS2Conflict := GlobalETS2Config.UserSelected and (GlobalETS2Config.ExistingFileFound) and
                       (GlobalETS2Config.OwnershipStatus <> OWNERSHIP_OWNED);

    HasATSConflict := GlobalATSConfig.UserSelected and (GlobalATSConfig.ExistingFileFound) and
                      (GlobalATSConfig.OwnershipStatus <> OWNERSHIP_OWNED);

    HasConflict := HasETS2Conflict or HasATSConflict;
    UpdateConflictPageVisibility();

    LogInfo('Conflict evaluation: HasETS2Conflict=' + BoolToStr(HasETS2Conflict) + ', HasATSConflict=' + BoolToStr(HasATSConflict));
  end;

  // Conflict resolution selection per game
  if CurPageID = ConflictPage.ID then
  begin
    if HasETS2Conflict then
    begin
      if ETS2ConflictOptionBackup.Checked then
        GlobalETS2Config.ConflictAction := CONFLICT_ACTION_BACKUP_REPLACE
      else if ETS2ConflictOptionKeep.Checked then
        GlobalETS2Config.ConflictAction := CONFLICT_ACTION_KEEP_EXISTING
      else
        GlobalETS2Config.ConflictAction := CONFLICT_ACTION_OVERWRITE;
      LogInfo('ETS2 conflict action selected: ' + IntToStr(GlobalETS2Config.ConflictAction));
    end
    else
      GlobalETS2Config.ConflictAction := CONFLICT_ACTION_NONE;

    if HasATSConflict then
    begin
      if ATSConflictOptionBackup.Checked then
        GlobalATSConfig.ConflictAction := CONFLICT_ACTION_BACKUP_REPLACE
      else if ATSConflictOptionKeep.Checked then
        GlobalATSConfig.ConflictAction := CONFLICT_ACTION_KEEP_EXISTING
      else
        GlobalATSConfig.ConflictAction := CONFLICT_ACTION_OVERWRITE;
      LogInfo('ATS conflict action selected: ' + IntToStr(GlobalATSConfig.ConflictAction));
    end
    else
      GlobalATSConfig.ConflictAction := CONFLICT_ACTION_NONE;
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

  if HasETS2Conflict then
  begin
    S := S + #13#10 + 'ETS2 Conflict Action:' + #13#10;
    if ETS2ConflictOptionBackup.Checked then
      S := S + '  ' + CustomMessage('ConflictOptionBackup') + #13#10
    else if ETS2ConflictOptionKeep.Checked then
      S := S + '  ' + CustomMessage('ConflictOptionKeep') + #13#10
    else
      S := S + '  ' + CustomMessage('ConflictOptionOverwrite') + #13#10;
  end;

  if HasATSConflict then
  begin
    S := S + #13#10 + 'ATS Conflict Action:' + #13#10;
    if ATSConflictOptionBackup.Checked then
      S := S + '  ' + CustomMessage('ConflictOptionBackup') + #13#10
    else if ATSConflictOptionKeep.Checked then
      S := S + '  ' + CustomMessage('ConflictOptionKeep') + #13#10
    else
      S := S + '  ' + CustomMessage('ConflictOptionOverwrite') + #13#10;
  end;

  Result := S;
end;
