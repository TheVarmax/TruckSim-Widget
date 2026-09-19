using System.Runtime.InteropServices;
using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.Common;

public static class ShellHelper
{
    public static void CreateShortcut(string shortcutPath, string targetPath, string arguments = "", string description = "", string iconPath = "")
    {
        try
        {
            string? dir = Path.GetDirectoryName(shortcutPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Using COM WScript.Shell
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return;

            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            string? targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir)) shortcut.WorkingDirectory = targetDir;
            if (!string.IsNullOrEmpty(arguments)) shortcut.Arguments = arguments;
            if (!string.IsNullOrEmpty(description)) shortcut.Description = description;
            if (!string.IsNullOrEmpty(iconPath)) shortcut.IconLocation = iconPath;
            shortcut.Save();

            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);

            InstallerLogger.LogInfo($"Created shortcut: {shortcutPath} -> {targetPath}");
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Failed to create shortcut {shortcutPath}: {ex.Message}");
        }
    }

    public static void RemoveShortcut(string shortcutPath)
    {
        try
        {
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
                InstallerLogger.LogInfo($"Removed shortcut: {shortcutPath}");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Failed to remove shortcut {shortcutPath}: {ex.Message}");
        }
    }

    public static void CreateAppShortcuts(string appDir, bool createDesktopShortcut)
    {
        string exePath = Path.Combine(appDir, Constants.AppExeName);
        string startMenuDir = Constants.GetStartMenuProgramsDir();

        // 1. Start Menu App Shortcut
        string appShortcut = Path.Combine(startMenuDir, $"{Constants.AppName}.lnk");
        CreateShortcut(appShortcut, exePath, description: "Launch TruckSim Widget", iconPath: exePath);

        // 2. Desktop Shortcut
        if (createDesktopShortcut)
        {
            string desktopShortcut = Path.Combine(Constants.GetDesktopDir(), $"{Constants.AppName}.lnk");
            CreateShortcut(desktopShortcut, exePath, description: "Launch TruckSim Widget", iconPath: exePath);
        }
    }

    public static void RemoveAppShortcuts()
    {
        string startMenuDir = Constants.GetStartMenuProgramsDir();
        string appShortcut = Path.Combine(startMenuDir, $"{Constants.AppName}.lnk");
        RemoveShortcut(appShortcut);

        // If Start Menu folder is empty, remove it
        try
        {
            if (Directory.Exists(startMenuDir) && !Directory.EnumerateFileSystemEntries(startMenuDir).Any())
            {
                Directory.Delete(startMenuDir);
            }
        }
        catch { }

        // Remove desktop shortcut if present
        string desktopShortcut = Path.Combine(Constants.GetDesktopDir(), $"{Constants.AppName}.lnk");
        RemoveShortcut(desktopShortcut);
    }
}
