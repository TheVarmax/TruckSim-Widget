using System.Diagnostics;
using TruckSimWidgetSetup.Common;

namespace TruckSimWidgetSetup.GameDiscovery;

public static class GameValidator
{
    public static bool ValidateGameFolder(string gamePath, string exeName, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(gamePath))
        {
            errorMessage = "Please select a game folder, or uncheck this game.";
            return false;
        }

        string normPath = Path.GetFullPath(gamePath).TrimEnd('\\');
        if (!Directory.Exists(normPath))
        {
            errorMessage = $"The specified folder does not exist:\n\n{normPath}";
            return false;
        }

        string binDir = Path.Combine(normPath, Constants.GameRelBinDir);
        if (!Directory.Exists(binDir))
        {
            errorMessage = $"This folder does not contain {Constants.GameRelBinDir}:\n\n{normPath}";
            return false;
        }

        string expectedExe = Path.Combine(binDir, exeName);
        if (!File.Exists(expectedExe))
        {
            errorMessage = $"Expected game executable was not found in {Constants.GameRelBinDir}:\n\n{normPath}\\{exeName}";
            return false;
        }

        return true;
    }

    public static bool IsGameProcessRunning(string exeName)
    {
        string procName = Path.GetFileNameWithoutExtension(exeName);
        try
        {
            var procs = Process.GetProcessesByName(procName);
            return procs.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsDirectoryWritable(string dirPath)
    {
        try
        {
            string targetDir = dirPath;
            if (!Directory.Exists(targetDir))
            {
                string? parent = Path.GetDirectoryName(targetDir);
                if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                    return false;
                targetDir = parent;
            }

            string testFile = Path.Combine(targetDir, $"__trucksim_write_test_{Environment.TickCount}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
