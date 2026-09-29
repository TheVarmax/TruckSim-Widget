using System.Diagnostics;
using System.Security.Cryptography;
using TruckSimWidgetSetup.Diagnostics;
using TruckSimWidgetSetup.FileManager;

namespace TruckSimWidgetSetup.Common;

public static class ElevatedHelperRunner
{
    private static string? _helperPath;
    private static string? _expectedSha256;

    public static void Reset()
    {
        _helperPath = null;
        _expectedSha256 = null;
    }

    public static void Initialize(string helperPath, string expectedSha256)
    {
        _helperPath = helperPath;
        _expectedSha256 = expectedSha256;
    }

    public static bool Execute(string command, string path1, string path2 = "", string allowRoot = "")
    {
        string? helperExe = _helperPath;

        if (string.IsNullOrWhiteSpace(helperExe) || string.IsNullOrWhiteSpace(_expectedSha256)
            || _expectedSha256.Length != 64 || !_expectedSha256.All(Uri.IsHexDigit))
        {
            InstallerLogger.LogErr("ElevatedHelper has no trusted manifest path and SHA-256 digest.");
            return false;
        }

        if (!File.Exists(helperExe))
        {
            InstallerLogger.LogErr($"ElevatedHelper.exe not found at: {helperExe}");
            return false;
        }

        try
        {
            // Keep the verified file open through launch: a replacement or write
            // between hashing and ShellExecute must be denied by Windows sharing.
            using var verifiedHelper = new FileStream(helperExe, FileMode.Open, FileAccess.Read, FileShare.Read);
            string actualHash = Convert.ToHexString(SHA256.HashData(verifiedHelper));
            if (!string.Equals(actualHash, _expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                InstallerLogger.LogErr($"ElevatedHelper.exe integrity verification failed! Expected {_expectedSha256}, got {actualHash}");
                return false;
            }
            InstallerLogger.LogInfo($"Executing ElevatedHelper operation: {command}");
            var psi = new ProcessStartInfo
            {
                FileName = helperExe,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            psi.ArgumentList.Add(command);
            psi.ArgumentList.Add(path1);
            if (!string.IsNullOrEmpty(path2)) psi.ArgumentList.Add(path2);
            if (!string.IsNullOrEmpty(allowRoot))
            {
                psi.ArgumentList.Add("--allow-root");
                psi.ArgumentList.Add(allowRoot);
            }

            using var proc = Process.Start(psi);
            if (proc == null) return false;

            proc.WaitForExit();
            if (proc.ExitCode == 0)
            {
                InstallerLogger.LogInfo($"ElevatedHelper operation ({command}) succeeded.");
                return true;
            }
            else
            {
                InstallerLogger.LogWarn($"ElevatedHelper operation ({command}) returned exit code: {proc.ExitCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"User cancelled UAC or ElevatedHelper failed: {ex.Message}");
            return false;
        }
    }

    public static bool CopyFileElevated(string source, string destination, string? allowRoot = null) =>
        Execute("copy", source, destination, allowRoot ?? ExtractGameRoot(destination));

    public static bool DeleteFileElevated(string target, string? allowRoot = null) =>
        Execute("delete", target, "", allowRoot ?? ExtractGameRoot(target));

    public static bool MkDirElevated(string dir, string? allowRoot = null) =>
        Execute("mkdir", dir, "", allowRoot ?? ExtractGameRoot(dir));

    public static bool RmDirElevated(string dir, string? allowRoot = null) =>
        Execute("rmdir", dir, "", allowRoot ?? ExtractGameRoot(dir));

    private static string ExtractGameRoot(string path)
    {
        int idx = path.IndexOf(@"\bin\win_x64\plugins", StringComparison.OrdinalIgnoreCase);
        if (idx > 0)
        {
            return path.Substring(0, idx);
        }
        return string.Empty;
    }
}
