// SteamDetection.iss - Robust Multi-Drive Steam & Game Detection
// Part of TruckSim Widget Installer

[Code]
function GetDriveType(lpRootPathName: String): UINT;
  external 'GetDriveTypeW@kernel32.dll stdcall';

procedure AddUniqueString(var Arr: TArrayOfString; const Val: String);
var
  I: Integer;
  NormVal: String;
begin
  NormVal := NormalizePath(Val);
  if NormVal = '' then exit;

  for I := 0 to GetArrayLength(Arr) - 1 do
  begin
    if CompareText(Arr[I], NormVal) = 0 then
      exit;
  end;

  SetArrayLength(Arr, GetArrayLength(Arr) + 1);
  Arr[GetArrayLength(Arr) - 1] := NormVal;
end;

function ExtractQuotedValueAfterKey(const Line, Key: String): String;
var
  P, StartPos, EndPos: Integer;
  KeyWithQuotes: String;
begin
  Result := '';
  KeyWithQuotes := '"' + Key + '"';
  P := Pos(LowerCase(KeyWithQuotes), LowerCase(Line));
  if P = 0 then
  begin
    // Try without quotes
    P := Pos(LowerCase(Key), LowerCase(Line));
    if P = 0 then exit;
    P := P + Length(Key);
  end
  else
    P := P + Length(KeyWithQuotes);

  // Find first quote after key
  StartPos := 0;
  while P <= Length(Line) do
  begin
    if Line[P] = '"' then
    begin
      StartPos := P + 1;
      Break;
    end;
    P := P + 1;
  end;

  if StartPos = 0 then exit;

  // Find closing quote
  EndPos := 0;
  P := StartPos;
  while P <= Length(Line) do
  begin
    if Line[P] = '"' then
    begin
      EndPos := P;
      Break;
    end;
    P := P + 1;
  end;

  if (StartPos > 0) and (EndPos >= StartPos) then
    Result := Copy(Line, StartPos, EndPos - StartPos);
end;

procedure GetSteamRoots(var Roots: TArrayOfString);
var
  RegVal: String;
begin
  SetArrayLength(Roots, 0);

  // Check HKCU SteamPath
  if RegQueryStringValue(HKCU, 'Software\Valve\Steam', 'SteamPath', RegVal) then
    AddUniqueString(Roots, RegVal);

  // Check HKLM 64-bit InstallPath
  if RegQueryStringValue(HKLM, 'SOFTWARE\Valve\Steam', 'InstallPath', RegVal) then
    AddUniqueString(Roots, RegVal);

  // Check HKLM 32-bit (WOW6432Node) InstallPath
  if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Valve\Steam', 'InstallPath', RegVal) then
    AddUniqueString(Roots, RegVal);

  // Standard Program Files locations
  AddUniqueString(Roots, ExpandConstant('{pf}\Steam'));
  AddUniqueString(Roots, ExpandConstant('{pf32}\Steam'));
end;

procedure ParseLibraryFoldersVdf(const SteamRoot: String; var Libraries: TArrayOfString);
var
  VdfPath: String;
  Lines: TArrayOfString;
  I: Integer;
  Line: String;
  PathVal: String;
begin
  VdfPath := CombinePath(CombinePath(SteamRoot, 'steamapps'), 'libraryfolders.vdf');
  if not SafeFileExists(VdfPath) then
  begin
    LogDebug('libraryfolders.vdf not found at: ' + VdfPath);
    exit;
  end;

  LogInfo('Parsing Steam library folders from: ' + VdfPath);
  if LoadStringsFromFile(VdfPath, Lines) then
  begin
    for I := 0 to GetArrayLength(Lines) - 1 do
    begin
      Line := Lines[I];
      if Pos('"path"', LowerCase(Line)) > 0 then
      begin
        PathVal := ExtractQuotedValueAfterKey(Line, 'path');
        if PathVal <> '' then
        begin
          StringChange(PathVal, '\\', '\');
          if SafeDirExists(PathVal) then
          begin
            LogInfo('Found Steam library folder: ' + PathVal);
            AddUniqueString(Libraries, PathVal);
          end;
        end;
      end;
    end;
  end;
end;

function CheckGameInLibrary(const LibraryPath: String; const AppId: Integer; const ExeName, DefaultDirName: String): String;
var
  AcfPath: String;
  AcfLines: TArrayOfString;
  I: Integer;
  InstallDirVal: String;
  CandidatePath: String;
  ExpectedExe: String;
begin
  Result := '';
  InstallDirVal := '';

  // 1. Probe appmanifest_<AppId>.acf
  AcfPath := CombinePath(CombinePath(LibraryPath, 'steamapps'), 'appmanifest_' + IntToStr(AppId) + '.acf');
  if SafeFileExists(AcfPath) then
  begin
    LogDebug('Found appmanifest: ' + AcfPath);
    if LoadStringsFromFile(AcfPath, AcfLines) then
    begin
      for I := 0 to GetArrayLength(AcfLines) - 1 do
      begin
        if Pos('"installdir"', LowerCase(AcfLines[I])) > 0 then
        begin
          InstallDirVal := ExtractQuotedValueAfterKey(AcfLines[I], 'installdir');
          if InstallDirVal <> '' then Break;
        end;
      end;
    end;
  end;

  // 2. If appmanifest gave installdir, verify it
  if InstallDirVal <> '' then
  begin
    CandidatePath := CombinePath(CombinePath(CombinePath(LibraryPath, 'steamapps'), 'common'), InstallDirVal);
    ExpectedExe := CombinePath(CombinePath(CandidatePath, GAME_REL_BIN_DIR), ExeName);
    if SafeFileExists(ExpectedExe) then
    begin
      Result := CandidatePath;
      LogInfo('Verified game via appmanifest: ' + Result);
      exit;
    end;
  end;

  // 3. Fallback to default game folder name
  CandidatePath := CombinePath(CombinePath(CombinePath(LibraryPath, 'steamapps'), 'common'), DefaultDirName);
  ExpectedExe := CombinePath(CombinePath(CandidatePath, GAME_REL_BIN_DIR), ExeName);
  if SafeFileExists(ExpectedExe) then
  begin
    Result := CandidatePath;
    LogInfo('Verified game via default common folder: ' + Result);
    exit;
  end;
end;

procedure GetFixedDrives(var Drives: TArrayOfString);
var
  I: Integer;
  DriveLetter: String;
  RootPath: String;
begin
  SetArrayLength(Drives, 0);
  for I := Ord('C') to Ord('Z') do
  begin
    DriveLetter := Chr(I);
    RootPath := DriveLetter + ':\';
    if GetDriveType(RootPath) = DRIVE_FIXED then
      AddUniqueString(Drives, DriveLetter + ':');
  end;
end;

function DetectGameInstallation(const GameId: String; const AppId: Integer; const ExeName, DefaultDirName: String; var AllFoundPaths: TArrayOfString): String;
var
  SteamRoots: TArrayOfString;
  Libraries: TArrayOfString;
  FixedDrives: TArrayOfString;
  I: Integer;
  Found: String;
  RegUninstallPath: String;
  CandidateExe: String;
  DrivePath: String;
begin
  SetArrayLength(AllFoundPaths, 0);
  Result := '';

  LogInfo('Starting Steam discovery pipeline for ' + GameId + ' (AppId: ' + IntToStr(AppId) + ')...');

  // Step 1: Collect Steam installation roots
  GetSteamRoots(SteamRoots);
  for I := 0 to GetArrayLength(SteamRoots) - 1 do
  begin
    AddUniqueString(Libraries, SteamRoots[I]);
    ParseLibraryFoldersVdf(SteamRoots[I], Libraries);
  end;

  // Step 2: Check each detected Steam library
  for I := 0 to GetArrayLength(Libraries) - 1 do
  begin
    Found := CheckGameInLibrary(Libraries[I], AppId, ExeName, DefaultDirName);
    if Found <> '' then
    begin
      AddUniqueString(AllFoundPaths, Found);
      if Result = '' then
        Result := Found;
    end;
  end;

  // Step 3: Check Windows Steam App Uninstall registry entry
  RegUninstallPath := '';
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App ' + IntToStr(AppId), 'InstallLocation', RegUninstallPath) or
     RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App ' + IntToStr(AppId), 'InstallLocation', RegUninstallPath) or
     RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\Steam App ' + IntToStr(AppId), 'InstallLocation', RegUninstallPath) then
  begin
    RegUninstallPath := NormalizePath(RegUninstallPath);
    if RegUninstallPath <> '' then
    begin
      CandidateExe := CombinePath(CombinePath(RegUninstallPath, GAME_REL_BIN_DIR), ExeName);
      if SafeFileExists(CandidateExe) then
      begin
        LogInfo('Found verified game from Steam App registry: ' + RegUninstallPath);
        AddUniqueString(AllFoundPaths, RegUninstallPath);
        if Result = '' then
          Result := RegUninstallPath;
      end
      else
        LogDebug('Steam App registry pointed to missing executable: ' + CandidateExe);
    end;
  end;

  // Step 4: Fallback scan on local fixed drives
  GetFixedDrives(FixedDrives);
  for I := 0 to GetArrayLength(FixedDrives) - 1 do
  begin
    DrivePath := FixedDrives[I];
    // Probe <Drive>:\SteamLibrary\steamapps\common\<Game>
    Found := CombinePath(CombinePath(CombinePath(DrivePath, 'SteamLibrary\steamapps\common'), DefaultDirName), '');
    if SafeFileExists(CombinePath(CombinePath(Found, GAME_REL_BIN_DIR), ExeName)) then
    begin
      AddUniqueString(AllFoundPaths, Found);
      if Result = '' then Result := Found;
    end;

    // Probe <Drive>:\Games\SteamLibrary\steamapps\common\<Game>
    Found := CombinePath(CombinePath(CombinePath(DrivePath, 'Games\SteamLibrary\steamapps\common'), DefaultDirName), '');
    if SafeFileExists(CombinePath(CombinePath(Found, GAME_REL_BIN_DIR), ExeName)) then
    begin
      AddUniqueString(AllFoundPaths, Found);
      if Result = '' then Result := Found;
    end;

    // Probe <Drive>:\Steam\steamapps\common\<Game>
    Found := CombinePath(CombinePath(CombinePath(DrivePath, 'Steam\steamapps\common'), DefaultDirName), '');
    if SafeFileExists(CombinePath(CombinePath(Found, GAME_REL_BIN_DIR), ExeName)) then
    begin
      AddUniqueString(AllFoundPaths, Found);
      if Result = '' then Result := Found;
    end;
  end;

  if Result <> '' then
    LogInfo('Discovery result for ' + GameId + ': ' + Result + ' (Total candidates: ' + IntToStr(GetArrayLength(AllFoundPaths)) + ')')
  else
    LogInfo('No verified installation found for ' + GameId);
end;
