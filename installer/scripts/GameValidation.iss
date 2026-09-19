// GameValidation.iss - Game Folder Validation, Process Locking & Permission Checks
// Part of TruckSim Widget Installer

[Code]
type
  TProcessEntry32 = record
    dwSize: DWORD;
    cntUsage: DWORD;
    th32ProcessID: DWORD;
    th32DefaultHeapID: Cardinal;
    th32ModuleID: DWORD;
    cntThreads: DWORD;
    th32ParentProcessID: DWORD;
    pcPriClassBase: Longint;
    dwFlags: DWORD;
    szExeFile: array[0..259] of Char;
  end;

function CreateToolhelp32Snapshot(dwFlags, th32ProcessID: DWORD): THandle;
  external 'CreateToolhelp32Snapshot@kernel32.dll stdcall';
function Process32First(hSnapshot: THandle; var lppe: TProcessEntry32): BOOL;
  external 'Process32FirstW@kernel32.dll stdcall';
function Process32Next(hSnapshot: THandle; var lppe: TProcessEntry32): BOOL;
  external 'Process32NextW@kernel32.dll stdcall';
function CloseHandle(hObject: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';
function GetTickCount(): DWORD;
  external 'GetTickCount@kernel32.dll stdcall';

function CharArrayToString(const Chars: array of Char): String;
var
  I: Integer;
begin
  Result := '';
  for I := 0 to GetArrayLength(Chars) - 1 do
  begin
    if Chars[I] = #0 then Break;
    Result := Result + Chars[I];
  end;
end;

function IsGameProcessRunning(const ExeName: String): Boolean;
var
  hSnap: THandle;
  pe: TProcessEntry32;
  Name: String;
begin
  Result := False;
  hSnap := CreateToolhelp32Snapshot($00000002, 0); // TH32CS_SNAPPROCESS = 2
  if hSnap = -1 then exit;
  try
    pe.dwSize := 556;
    if Process32First(hSnap, pe) then
    begin
      repeat
        Name := CharArrayToString(pe.szExeFile);
        if CompareText(Name, ExeName) = 0 then
        begin
          Result := True;
          Break;
        end;
      until not Process32Next(hSnap, pe);
    end;
  finally
    CloseHandle(hSnap);
  end;
end;

function IsDirectoryWritable(const DirPath: String): Boolean;
var
  TestFile: String;
  TargetDir: String;
begin
  Result := False;
  TargetDir := NormalizePath(DirPath);
  if not SafeDirExists(TargetDir) then
  begin
    // If target directory doesn't exist, check parent
    TargetDir := ExtractFilePath(TargetDir);
    if not SafeDirExists(TargetDir) then exit;
  end;

  TestFile := CombinePath(TargetDir, '__trucksim_write_test_' + IntToStr(GetTickCount) + '.tmp');
  try
    if SaveStringToFile(TestFile, 'write_test', False) then
    begin
      DeleteFile(TestFile);
      Result := True;
    end;
  except
    Result := False;
  end;
end;

function ValidateGameFolder(const GamePath, ExeName: String; var Reason: String): Boolean;
var
  NormPath: String;
  BinDir: String;
  ExpectedExe: String;
begin
  Result := False;
  Reason := '';
  NormPath := NormalizePath(GamePath);

  if NormPath = '' then
  begin
    Reason := CustomMessage('ErrPathEmpty');
    exit;
  end;

  if not SafeDirExists(NormPath) then
  begin
    Reason := CustomMessage('ErrDirNotExist');
    exit;
  end;

  BinDir := CombinePath(NormPath, GAME_REL_BIN_DIR);
  if not SafeDirExists(BinDir) then
  begin
    Reason := FmtMessage(CustomMessage('ErrBinDirMissing'), [NormPath, GAME_REL_BIN_DIR]);
    exit;
  end;

  ExpectedExe := CombinePath(BinDir, ExeName);
  if not SafeFileExists(ExpectedExe) then
  begin
    Reason := FmtMessage(CustomMessage('ErrExeMissing'), [NormPath, ExeName]);
    exit;
  end;

  Result := True;
end;

function CopyFileElevated(const SourceFile, TargetFile: String): Boolean;
var
  CmdParams: String;
  ResultCode: Integer;
begin
  Result := False;
  CmdParams := '/c copy /y "' + SourceFile + '" "' + TargetFile + '"';
  LogInfo('Requesting targeted elevation to copy plugin: ' + SourceFile + ' -> ' + TargetFile);
  
  if ShellExec('runas', 'cmd.exe', CmdParams, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if (ResultCode = 0) and SafeFileExists(TargetFile) then
    begin
      LogInfo('Targeted elevated copy succeeded.');
      Result := True;
    end
    else
      LogWarn('Targeted elevated copy failed with exit code: ' + IntToStr(ResultCode));
  end
  else
    LogWarn('User declined targeted elevation prompt or ShellExec failed.');
end;
