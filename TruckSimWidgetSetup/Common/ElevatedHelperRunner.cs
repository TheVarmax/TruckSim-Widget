using System.Diagnostics;
using System.Security.Cryptography;
using TruckSimWidgetSetup.Diagnostics;
using TruckSimWidgetSetup.FileManager;
using TruckSimWidgetSetup.PluginManager;

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
        => ExecuteVerified(command, path1, path2, allowRoot, _helperPath, _expectedSha256);

    private static bool ExecuteVerified(string command, string path1, string path2, string allowRoot,
        string? helperExe, string? expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(helperExe) || string.IsNullOrWhiteSpace(expectedSha256)
            || expectedSha256.Length != 64 || !expectedSha256.All(Uri.IsHexDigit))
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
            if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                InstallerLogger.LogErr($"ElevatedHelper.exe integrity verification failed! Expected {expectedSha256}, got {actualHash}");
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

    internal static bool CopyPluginFileElevated(string source, string destination)
    {
        return ExecutePluginOperation("copy", source, destination, ExtractGameRoot(destination));
    }

    internal static bool DeletePluginFileElevated(string target)
    {
        return ExecutePluginOperation("delete", target, "", ExtractGameRoot(target));
    }

    private static bool ExecutePluginOperation(string command, string path1, string path2, string root)
    {
        if (_helperPath != null) return Execute(command, path1, path2, root);
        return WithTrustedRecoveryHelper((helper, digest) => ExecuteVerified(command, path1, path2, root, helper, digest));
    }

    internal static bool WithTrustedRecoveryHelper(Func<string, string, bool> run)
    {
        // Recovery can precede the first saved install-state. Neither the journal
        // nor the installed manifest is a trust anchor for launching elevated code.
        // Always extract the helper from this installer's embedded payload.
        try
        {
            using var payload = new EmbeddedPayloadProvider();
            if (!payload.Manifest.TryGetEntry("ElevatedHelper.exe", out var entry) || entry == null) return false;
            using var staging = PluginStagingDirectory.Create();
            string helper = Path.Combine(staging.Path, "ElevatedHelper.exe");
            payload.ExtractFile("ElevatedHelper.exe", staging.Path, helper);
            using var verified = VerifiedPluginSource.Open(helper, entry.Sha256);
            return run(helper, entry.Sha256);
        }
        catch (Exception)
        {
            InstallerLogger.LogErr("A verified embedded helper could not be prepared for plugin recovery.");
            return false;
        }
    }

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
