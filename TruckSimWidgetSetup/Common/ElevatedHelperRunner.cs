using System.Diagnostics;
using TruckSimWidgetSetup.Diagnostics;
using TruckSimWidgetSetup.FileManager;

namespace TruckSimWidgetSetup.Common;

public static class ElevatedHelperRunner
{
    private static string? _helperPath;
    private static string? _expectedSha256;

    public static void Initialize(string helperPath, string? expectedSha256 = null)
    {
        _helperPath = helperPath;
        _expectedSha256 = expectedSha256;
    }

    public static bool Execute(string command, string path1, string path2 = "", string allowRoot = "")
    {
        string helperExe = _helperPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ElevatedHelper.exe");

        if (!File.Exists(helperExe))
        {
            InstallerLogger.LogErr($"ElevatedHelper.exe not found at: {helperExe}");
            return false;
        }

        // Verify SHA-256 integrity if expected hash is provided
        if (!string.IsNullOrEmpty(_expectedSha256))
        {
            string actualHash = PackageManifest.ComputeFileSha256(helperExe);
            if (!string.Equals(actualHash, _expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                InstallerLogger.LogErr($"ElevatedHelper.exe integrity verification failed! Expected {_expectedSha256}, got {actualHash}");
                return false;
            }
        }

        string cmdParams;
        if (!string.IsNullOrEmpty(path2))
            cmdParams = $"{command} \"{path1}\" \"{path2}\"";
        else
            cmdParams = $"{command} \"{path1}\"";

        if (!string.IsNullOrEmpty(allowRoot))
            cmdParams += $" --allow-root \"{allowRoot}\"";

        InstallerLogger.LogInfo($"Executing ElevatedHelper: {helperExe} {cmdParams}");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = helperExe,
                Arguments = cmdParams,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

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
