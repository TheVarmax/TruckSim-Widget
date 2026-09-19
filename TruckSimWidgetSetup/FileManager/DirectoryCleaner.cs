using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.FileManager;

public static class DirectoryCleaner
{
    /// <summary>
    /// Recursively removes empty subdirectories inside rootDir bottom-up.
    /// If rootDir itself becomes empty, removes it as well.
    /// NEVER removes a directory that contains any file (especially unknown/user files).
    /// </summary>
    public static void CleanEmptyDirectories(string rootDir)
    {
        if (!Directory.Exists(rootDir)) return;

        try
        {
            CleanEmptyDirsInternal(rootDir);

            // Check if rootDir itself is now completely empty
            if (Directory.Exists(rootDir) && !Directory.EnumerateFileSystemEntries(rootDir).Any())
            {
                Directory.Delete(rootDir, recursive: false);
                InstallerLogger.LogInfo($"Removed empty root directory: {rootDir}");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Error cleaning empty directories in {rootDir}: {ex.Message}");
        }
    }

    private static void CleanEmptyDirsInternal(string dir)
    {
        foreach (string subDir in Directory.GetDirectories(dir))
        {
            CleanEmptyDirsInternal(subDir);

            try
            {
                if (Directory.Exists(subDir) && !Directory.EnumerateFileSystemEntries(subDir).Any())
                {
                    Directory.Delete(subDir, recursive: false);
                    InstallerLogger.LogInfo($"Removed empty subdirectory: {subDir}");
                }
            }
            catch
            {
                // Directory may be locked or not empty
            }
        }
    }
}
