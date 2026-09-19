// Diagnostics.iss - Installer Logging & System Diagnostics
// Part of TruckSim Widget Installer

[Code]
function RedactPattern(const S, KeyPattern: String): String;
var
  P, StartPos, EndPos: Integer;
  ResultStr: String;
  LowerS: String;
begin
  ResultStr := S;
  LowerS := LowerCase(ResultStr);
  P := Pos(LowerCase(KeyPattern), LowerS);
  while P > 0 do
  begin
    StartPos := P + Length(KeyPattern);
    EndPos := StartPos;
    while (EndPos <= Length(ResultStr)) and (ResultStr[EndPos] <> ' ') and 
          (ResultStr[EndPos] <> '&') and (ResultStr[EndPos] <> ';') and 
          (ResultStr[EndPos] <> '"') and (ResultStr[EndPos] <> '''') do
      Inc(EndPos);
    if EndPos > StartPos then
    begin
      Delete(ResultStr, StartPos, EndPos - StartPos);
      Insert('[REDACTED]', ResultStr, StartPos);
    end;
    LowerS := LowerCase(ResultStr);
    P := Pos(LowerCase(KeyPattern), LowerS);
  end;
  Result := ResultStr;
end;

function SanitizeLogMessage(const Msg: String): String;
var
  S: String;
  UserName: String;
  UserPath: String;
begin
  S := Msg;

  try
    UserName := ExpandConstant('{username}');
    if (UserName <> '') and (Length(UserName) > 1) then
    begin
      UserPath := '\Users\' + UserName;
      StringChangeEx(S, UserPath, '\Users\<redacted>', True);
    end;
  except
  end;

  S := RedactPattern(S, 'token=');
  S := RedactPattern(S, 'password=');
  S := RedactPattern(S, 'secret=');
  S := RedactPattern(S, 'key=');
  S := RedactPattern(S, 'bearer ');

  Result := S;
end;

procedure LogInstallerMsg(const Level, Msg: String);
var
  LogFile: String;
  LogDir: String;
  Timestamp: String;
  CleanMsg: String;
  Line: String;
begin
  CleanMsg := SanitizeLogMessage(Msg);
  
  // Also pass to Inno Setup's internal log
  Log('[' + Level + '] ' + CleanMsg);

  try
    LogFile := GetInstallerLogFilePath();
    LogDir := ExtractFilePath(LogFile);
    
    if not DirExists(LogDir) then
      ForceDirectories(LogDir);

    Timestamp := GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':');
    Line := '[' + Timestamp + '] [' + Level + '] ' + CleanMsg + #13#10;
    
    SaveStringToFile(LogFile, Line, True);
  except
    // Logging failure must never crash the installer
  end;
end;

procedure LogInfo(const Msg: String);
begin
  LogInstallerMsg('INFO', Msg);
end;

procedure LogWarn(const Msg: String);
begin
  LogInstallerMsg('WARN', Msg);
end;

procedure LogErr(const Msg: String);
begin
  LogInstallerMsg('ERROR', Msg);
end;

procedure LogDebug(const Msg: String);
begin
  LogInstallerMsg('DEBUG', Msg);
end;

procedure InitInstallerLogging(const VersionStr: String);
begin
  LogInfo('================================================================');
  LogInfo('TruckSim Widget Installer Session Started');
  LogInfo('Installer Version : ' + VersionStr);
  LogInfo('Command Tail      : ' + GetCmdTail);
  LogInfo('Update Mode       : ' + BoolToStr(IsUpdateMode()));
  LogInfo('Is Admin / Elevated: ' + BoolToStr(IsAdmin()));
  LogInfo('Is 64-Bit OS      : ' + BoolToStr(Is64BitInstallMode()));
  LogInfo('AppData Directory : ' + GetAppDataWidgetDir());
  LogInfo('================================================================');
end;
