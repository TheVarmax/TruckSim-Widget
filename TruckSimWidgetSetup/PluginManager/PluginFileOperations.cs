using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.PluginManager;

internal enum PluginCopyResult { Success, CopyFailed, IntegrityFailed }

// The same read-only sharing contract as ElevatedHelperRunner and ElevatedHelper:
// neither writes nor delete/rename are shared until the helper has exited.
internal sealed class PluginFileOperations
{
    private readonly Action<string, string> _copy;
    private readonly Func<string, string, bool> _elevatedCopy;

    internal PluginFileOperations(Action<string, string>? copy = null, Func<string, string, bool>? elevatedCopy = null)
    {
        _copy = copy ?? ((source, target) => File.Copy(source, target, overwrite: true));
        _elevatedCopy = elevatedCopy ?? ElevatedHelperRunner.CopyPluginFileElevated;
    }

    internal PluginCopyResult CopyVerified(string source, string target, string expectedHash)
    {
        try
        {
            using var verified = VerifiedPluginSource.Open(source, expectedHash);
            try { _copy(source, target); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (!_elevatedCopy(source, target)) return PluginCopyResult.CopyFailed;
            }
            // Exit code is not evidence of the destination contents.
            using var destination = VerifiedPluginSource.Open(target, expectedHash);
            return PluginCopyResult.Success;
        }
        catch (InvalidDataException)
        {
            InstallerLogger.LogErr("Telemetry plugin SHA-256 verification failed.");
            return PluginCopyResult.IntegrityFailed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            InstallerLogger.LogErr("Telemetry plugin copy or source validation failed.");
            return PluginCopyResult.CopyFailed;
        }
    }

    internal static void StageVerified(string source, string destination, string expectedHash)
    {
        using var verified = VerifiedPluginSource.Open(source, expectedHash);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        verified.Stream.CopyTo(output);
        output.Flush(flushToDisk: true);
    }

    internal bool RestoreVerified(string source, string target, string expectedHash)
    {
        // Old LocalAppData journal backups and persistent game backups are read
        // without elevation, then restaged to a source accepted by the helper.
        using var staging = PluginStagingDirectory.Create();
        string staged = Path.Combine(staging.Path, Constants.PluginFileName);
        try
        {
            using var original = VerifiedPluginSource.Open(source, expectedHash);
            // Cancellation may have left the original target untouched. Verify
            // that state instead of requiring a second UAC prompt to rewrite it.
            try
            {
                using var unchanged = VerifiedPluginSource.Open(target, expectedHash);
                return true;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
            StageVerified(source, staged, expectedHash);
            return CopyVerified(staged, target, expectedHash) == PluginCopyResult.Success;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            InstallerLogger.LogErr("Telemetry plugin backup could not be verified or restored.");
            return false;
        }
    }
}

internal sealed class VerifiedPluginSource : IDisposable
{
    private readonly List<SafeFileHandle> _directories = new();
    internal FileStream Stream { get; private set; } = null!;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle OpenNoFollow(string path, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);

    [StructLayout(LayoutKind.Sequential)]
    private struct AttributeTag { public uint Attributes; public uint Tag; }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass,
        out AttributeTag info, uint size);

    internal static VerifiedPluginSource Open(string path, string expectedHash)
    {
        if (expectedHash.Length != 64 || !expectedHash.All(Uri.IsHexDigit))
            throw new InvalidDataException("Missing trusted plugin digest.");
        var result = new VerifiedPluginSource();
        try
        {
            string fullPath = System.IO.Path.GetFullPath(path);
            if (!System.IO.Path.IsPathFullyQualified(path) || fullPath.StartsWith(@"\\", StringComparison.Ordinal))
                throw new IOException("Plugin source must be a local absolute path.");

            // Pin every ancestor as well as the file. A file-only lease would
            // still allow an ancestor to be renamed/replaced before helper use.
            var ancestors = new Stack<string>();
            for (string? parent = System.IO.Path.GetDirectoryName(fullPath); parent != null;
                 parent = System.IO.Path.GetDirectoryName(parent)) ancestors.Push(parent);
            foreach (string directory in ancestors)
            {
                var handle = OpenChecked(directory, 0x80, 3, 0x02000000 | 0x00200000, isDirectory: true);
                result._directories.Add(handle); // read/write sharing, never delete sharing
            }
            var file = OpenChecked(fullPath, 0x80000000, 1, 0x00200000 | 0x08000000, isDirectory: false);
            try { result.Stream = new FileStream(file, FileAccess.Read); }
            catch { file.Dispose(); throw; }
            string actual = Convert.ToHexString(SHA256.HashData(result.Stream));
            if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Plugin digest mismatch.");
            result.Stream.Position = 0;
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    private static SafeFileHandle OpenChecked(string path, uint access, uint sharing, uint flags, bool isDirectory)
    {
        var handle = OpenNoFollow(path, access, sharing, IntPtr.Zero, 3, flags, IntPtr.Zero);
        try
        {
            if (handle.IsInvalid || !GetFileInformationByHandleEx(handle, 9, out var info, 8))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            if ((info.Attributes & (uint)FileAttributes.ReparsePoint) != 0 ||
                ((info.Attributes & (uint)FileAttributes.Directory) != 0) != isDirectory)
                throw new IOException("Plugin path contains a reparse point or unexpected file type.");
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }

    public void Dispose()
    {
        Stream?.Dispose();
        for (int i = _directories.Count - 1; i >= 0; i--) _directories[i].Dispose();
    }
}
