using System.IO;
using Microsoft.Win32;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.GameDiscovery;

public static class SteamFinder
{
    public static string DetectGameInstallation(string gameId, int appId, string exeName, string defaultDirName, out List<string> allFoundPaths)
    {
        allFoundPaths = new List<string>();
        string result = string.Empty;

        InstallerLogger.LogInfo($"Starting Steam discovery pipeline for {gameId} (AppId: {appId})...");

        // 1. Collect Steam roots
        var steamRoots = GetSteamRoots();
        var libraries = new List<string>();

        foreach (var root in steamRoots)
        {
            AddUniquePath(libraries, root);
            ParseLibraryFoldersVdf(root, libraries);
        }

        // 2. Check each detected Steam library
        foreach (var library in libraries)
        {
            string found = CheckGameInLibrary(library, appId, exeName, defaultDirName);
            if (!string.IsNullOrEmpty(found))
            {
                AddUniquePath(allFoundPaths, found);
                if (string.IsNullOrEmpty(result)) result = found;
            }
        }

        // 3. Check Windows Steam App Uninstall registry entries
        string regPath = CheckSteamAppRegistry(appId, exeName);
        if (!string.IsNullOrEmpty(regPath))
        {
            AddUniquePath(allFoundPaths, regPath);
            if (string.IsNullOrEmpty(result)) result = regPath;
        }

        // 4. Fallback scan on local fixed drives
        var fixedDrives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => d.RootDirectory.FullName.TrimEnd('\\'))
            .ToList();

        foreach (var drive in fixedDrives)
        {
            string[] probeTemplates =
            [
                Path.Combine(drive, "SteamLibrary", "steamapps", "common", defaultDirName),
                Path.Combine(drive, "Games", "SteamLibrary", "steamapps", "common", defaultDirName),
                Path.Combine(drive, "Steam", "steamapps", "common", defaultDirName)
            ];

            foreach (var probe in probeTemplates)
            {
                string expectedExe = Path.Combine(probe, Constants.GameRelBinDir, exeName);
                if (File.Exists(expectedExe))
                {
                    AddUniquePath(allFoundPaths, probe);
                    if (string.IsNullOrEmpty(result)) result = probe;
                }
            }
        }

        if (!string.IsNullOrEmpty(result))
        {
            InstallerLogger.LogInfo($"Discovery result for {gameId}: {result} (Total candidates: {allFoundPaths.Count})");
        }
        else
        {
            InstallerLogger.LogInfo($"No verified installation found for {gameId}");
        }

        return result;
    }

    private static List<string> GetSteamRoots()
    {
        var roots = new List<string>();

        // HKCU
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is string steamPath && Directory.Exists(steamPath))
            {
                AddUniquePath(roots, steamPath);
            }
        }
        catch { }

        // HKLM 64-bit
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
            if (key?.GetValue("InstallPath") is string installPath && Directory.Exists(installPath))
            {
                AddUniquePath(roots, installPath);
            }
        }
        catch { }

        // HKLM 32-bit (WOW6432Node)
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            if (key?.GetValue("InstallPath") is string installPath && Directory.Exists(installPath))
            {
                AddUniquePath(roots, installPath);
            }
        }
        catch { }

        // Standard Program Files
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pfX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        AddUniquePath(roots, Path.Combine(pf, "Steam"));
        AddUniquePath(roots, Path.Combine(pfX86, "Steam"));

        return roots;
    }

    private static void ParseLibraryFoldersVdf(string steamRoot, List<string> libraries)
    {
        string vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath)) return;

        try
        {
            var lines = File.ReadAllLines(vdfPath);
            foreach (var line in lines)
            {
                if (line.Contains("\"path\"", StringComparison.OrdinalIgnoreCase))
                {
                    string pathVal = ExtractQuotedValueAfterKey(line, "path");
                    if (!string.IsNullOrEmpty(pathVal))
                    {
                        pathVal = pathVal.Replace(@"\\", @"\");
                        if (Directory.Exists(pathVal))
                        {
                            AddUniquePath(libraries, pathVal);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Failed to parse {vdfPath}: {ex.Message}");
        }
    }

    private static string CheckGameInLibrary(string libraryPath, int appId, string exeName, string defaultDirName)
    {
        string acfPath = Path.Combine(libraryPath, "steamapps", $"appmanifest_{appId}.acf");
        string installDirVal = string.Empty;

        if (File.Exists(acfPath))
        {
            try
            {
                var lines = File.ReadAllLines(acfPath);
                foreach (var line in lines)
                {
                    if (line.Contains("\"installdir\"", StringComparison.OrdinalIgnoreCase))
                    {
                        installDirVal = ExtractQuotedValueAfterKey(line, "installdir");
                        if (!string.IsNullOrEmpty(installDirVal)) break;
                    }
                }
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(installDirVal))
        {
            string candidate = Path.Combine(libraryPath, "steamapps", "common", installDirVal);
            string expectedExe = Path.Combine(candidate, Constants.GameRelBinDir, exeName);
            if (File.Exists(expectedExe)) return candidate;
        }

        // Fallback to default game folder name
        string fallbackCandidate = Path.Combine(libraryPath, "steamapps", "common", defaultDirName);
        string fallbackExe = Path.Combine(fallbackCandidate, Constants.GameRelBinDir, exeName);
        if (File.Exists(fallbackExe)) return fallbackCandidate;

        return string.Empty;
    }

    private static string CheckSteamAppRegistry(int appId, string exeName)
    {
        string[] regKeys =
        [
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}",
            $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}"
        ];

        foreach (var subKey in regKeys)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(subKey) ?? Registry.CurrentUser.OpenSubKey(subKey);
                if (key?.GetValue("InstallLocation") is string loc && Directory.Exists(loc))
                {
                    string expectedExe = Path.Combine(loc, Constants.GameRelBinDir, exeName);
                    if (File.Exists(expectedExe)) return loc;
                }
            }
            catch { }
        }

        return string.Empty;
    }

    private static string ExtractQuotedValueAfterKey(string line, string key)
    {
        int p = line.IndexOf($"\"{key}\"", StringComparison.OrdinalIgnoreCase);
        if (p < 0)
        {
            p = line.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (p < 0) return string.Empty;
            p += key.Length;
        }
        else
        {
            p += key.Length + 2;
        }

        int start = line.IndexOf('"', p);
        if (start < 0) return string.Empty;

        int end = line.IndexOf('"', start + 1);
        if (end < 0) return string.Empty;

        return line.Substring(start + 1, end - start - 1);
    }

    private static void AddUniquePath(List<string> list, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        string norm = Path.GetFullPath(path).TrimEnd('\\');
        if (!list.Any(p => string.Equals(p, norm, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(norm);
        }
    }
}
