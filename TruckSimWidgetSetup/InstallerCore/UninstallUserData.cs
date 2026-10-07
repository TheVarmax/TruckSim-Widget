using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.InstallerCore;

internal static class UninstallUserData
{
    internal static void Remove(string appDir, string localAppData, Action removeSettings)
    {
        // Only known runtime files are removed from the application directory.
        // Unknown files there and third-party game data retain their ownership rules.
        foreach (string relative in new[]
        {
            "device.dat", "device.dat.tmp", "state.dat", "state.dat.tmp", "app_log.txt",
            Path.Combine("Resources", "city_translations.json")
        })
        {
            string path = Path.Combine(appDir, relative);
            RequireLocalDirectory(Path.GetDirectoryName(path)!);
            File.Delete(path);
        }
        RemoveEmptyDirectory(Path.Combine(appDir, "Resources"));
        RemoveEmptyDirectory(appDir);
        removeSettings();

        // No later status/self-delete log may recreate the deleted installer folder.
        InstallerLogger.SetFileLoggingEnabled(false);
        try
        {
            foreach (string name in new[] { "TruckSimWidget", "TruckSim Widget" })
            {
                string path = Path.Combine(localAppData, name);
                RequireLocalDirectory(path);
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            InstallerLogger.SetFileLoggingEnabled(true);
            throw;
        }
    }

    private static void RequireLocalDirectory(string path)
    {
        string full = Path.GetFullPath(path);
        if (!Path.IsPathFullyQualified(path) || full.StartsWith(@"\\", StringComparison.Ordinal)
            || string.Equals(full.TrimEnd(Path.DirectorySeparatorChar),
                Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new IOException("User data cleanup requires a local directory.");
        for (string? current = full; current != null; current = Path.GetDirectoryName(current))
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("User data cleanup cannot traverse a reparse point.");
    }

    private static void RemoveEmptyDirectory(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            Directory.Delete(path, recursive: false);
    }
}
