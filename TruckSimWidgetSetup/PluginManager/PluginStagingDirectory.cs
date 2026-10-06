using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.PluginManager;

internal sealed class PluginStagingDirectory : IDisposable
{
    private const string Prefix = "TruckSimWidget_Staging_";
    internal string Path { get; }
    private PluginStagingDirectory(string path) => Path = path;

    internal static PluginStagingDirectory Create() => new(Directory.CreateTempSubdirectory(Prefix).FullName);

    internal static bool IsOwnedPath(string path)
    {
        try
        {
            string full = System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            string temp = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            return !full.StartsWith(@"\\", StringComparison.Ordinal) &&
                string.Equals(System.IO.Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase) &&
                System.IO.Path.GetFileName(full).StartsWith(Prefix, StringComparison.Ordinal) &&
                System.IO.Path.GetFileName(full).Length > Prefix.Length;
        }
        catch { return false; }
    }

    internal static void Cleanup(string path)
    {
        if (!IsOwnedPath(path)) return;
        try
        {
            if (!Directory.Exists(path)) return;
            // Never traverse a replaced directory/junction, including nested entries.
            string? parent = System.IO.Path.GetFullPath(path);
            while (parent != null)
            {
                if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) return;
                parent = System.IO.Path.GetDirectoryName(parent);
            }
            var files = Directory.GetFileSystemEntries(path);
            if (files.Any(file => (File.GetAttributes(file) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)) return;
            foreach (var file in files) File.Delete(file);
            Directory.Delete(path, recursive: false);
        }
        catch { InstallerLogger.LogWarn("Telemetry staging cleanup could not finish; files retained for recovery."); }
    }

    public void Dispose() => Cleanup(Path);
}
