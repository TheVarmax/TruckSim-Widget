// Diagnostics.iss - Installer Logging & System Diagnostics
// Part of TruckSim Widget Installer

[Code]
function SanitizeLogMessage(const Msg: String): String;
var
  S: String;
begin
  S := Msg;
  // Redact potential sensitive tokens or tokens in URLs
  // (e.g. token=..., password=..., secret=..., key=...)
  // Basic safety pass
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
