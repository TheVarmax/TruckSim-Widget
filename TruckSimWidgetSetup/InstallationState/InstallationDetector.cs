using System.Diagnostics;
using Microsoft.Win32;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.InstallationState;

public static class InstallationDetector
{
    public static InstallationInfo Detect()
    {
        var info = new InstallationInfo();

        // 1. Load install state if present
        string stateFile = Constants.GetInstallStateFilePath();
        InstallStateModel? state = InstallStateModel.LoadFromFile(stateFile);
        info.ExistingState = state;

        string regDisplayVersion = string.Empty;
        string regUninstallString = string.Empty;

        // 2. Collect candidate directories in order of priority
        var candidates = new List<string>();

        // Priority A: State file install path
        if (state != null && !string.IsNullOrWhiteSpace(state.InstallPath))
        {
            candidates.Add(state.InstallPath.TrimEnd('\\'));
        }

        // Priority B: Registry uninstall keys (HKCU, HKLM, 64-bit and WOW6432Node)
        var regKeys = new[]
        {
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        };

        foreach (var (root, basePath) in regKeys)
        {
            try
            {
                using var baseKey = root.OpenSubKey(basePath);
                if (baseKey == null) continue;

                // Check exact AppId key first
                string exactSubKey = $"{Constants.AppId}_is1";
                using var exactKey = baseKey.OpenSubKey(exactSubKey);
                if (exactKey != null)
                {
                    ExtractRegistryCandidate(exactKey, candidates, ref regDisplayVersion, ref regUninstallString);
                }

                // Also inspect subkeys for "TruckSim Widget" display name
                foreach (string subKeyName in baseKey.GetSubKeyNames())
                {
                    if (string.Equals(subKeyName, exactSubKey, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        using var subKey = baseKey.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        string dispName = subKey.GetValue("DisplayName") as string ?? string.Empty;
                        if (dispName.Contains(Constants.AppName, StringComparison.OrdinalIgnoreCase))
                        {
                            ExtractRegistryCandidate(subKey, candidates, ref regDisplayVersion, ref regUninstallString);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                InstallerLogger.LogWarn($"Error querying registry {root}\\{basePath}: {ex.Message}");
            }
        }

        // Priority C: Default application directory
        candidates.Add(Constants.GetDefaultAppDir().TrimEnd('\\'));

        // 3. Search candidate directories for an existing TruckSim Widget.exe
        foreach (string dir in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;

            string exePath = Path.Combine(dir, Constants.AppExeName);
            if (File.Exists(exePath))
            {
                info.InstallPath = dir;

                // Determine version
                if (!string.IsNullOrEmpty(regDisplayVersion))
                {
                    info.InstalledVersion = regDisplayVersion;
                }
                else if (state != null && !string.IsNullOrEmpty(state.InstallerVersion))
                {
                    info.InstalledVersion = state.InstallerVersion;
                }
                else
                {
                    try
                    {
                        var vi = FileVersionInfo.GetVersionInfo(exePath);
                        string ver = vi.ProductVersion ?? vi.FileVersion ?? string.Empty;
                        int plusIdx = ver.IndexOf('+');
                        if (plusIdx > 0) ver = ver.Substring(0, plusIdx);
                        info.InstalledVersion = ver;
                    }
                    catch
                    {
                        info.InstalledVersion = "Unknown";
                    }
                }

                info.LegacyUninstallString = regUninstallString;

                // Check if legacy Inno
                bool hasInnoExe = File.Exists(Path.Combine(dir, "unins000.exe"));
                bool isInnoUninstaller = !string.IsNullOrEmpty(regUninstallString) &&
                                         regUninstallString.Contains("unins", StringComparison.OrdinalIgnoreCase);

                if (hasInnoExe || isInnoUninstaller || state == null || state.InstalledFiles.Count == 0)
                {
                    info.Status = InstallationStatus.LegacyInno;
                    info.IsLegacyInno = true;
                    InstallerLogger.LogInfo($"Detected Legacy Inno installation at: {dir} (Version: {info.InstalledVersion})");
                }
                else
                {
                    info.Status = InstallationStatus.Installed;
                    info.IsLegacyInno = false;
                    InstallerLogger.LogInfo($"Detected valid modern installation at: {dir} (Version: {info.InstalledVersion})");
                }

                return info;
            }
        }

        // 4. If executable was not found, check for stale directory in default location
        string defaultDir = Constants.GetDefaultAppDir();
        if (Directory.Exists(defaultDir) && Directory.EnumerateFileSystemEntries(defaultDir).Any())
        {
            info.InstallPath = defaultDir;
            info.Status = InstallationStatus.StaleDirectory;
            InstallerLogger.LogInfo($"Detected stale directory at: {defaultDir} (treating as fresh install, unknown files will be preserved).");
            return info;
        }

        // 5. Fresh installation
        info.InstallPath = defaultDir;
        info.Status = InstallationStatus.NotInstalled;
        InstallerLogger.LogInfo($"No existing installation detected. Target install path: {defaultDir}");
        return info;
    }

    private static void ExtractRegistryCandidate(
        RegistryKey key,
        List<string> candidates,
        ref string regDisplayVersion,
        ref string regUninstallString)
    {
        string installLoc = key.GetValue("InstallLocation") as string ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(installLoc))
        {
            candidates.Add(installLoc.Trim().TrimEnd('\\'));
        }

        string innoPath = key.GetValue("Inno Setup: App Path") as string ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(innoPath))
        {
            candidates.Add(innoPath.Trim().TrimEnd('\\'));
        }

        string uninst = key.GetValue("UninstallString") as string ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(uninst))
        {
            if (string.IsNullOrEmpty(regUninstallString))
            {
                regUninstallString = uninst;
            }

            // Extract directory from uninstaller path if quoted or unquoted
            string rawPath = uninst.Trim().Trim('"');
            int exeIdx = rawPath.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx > 0)
            {
                rawPath = rawPath.Substring(0, exeIdx + 4);
                string? dir = Path.GetDirectoryName(rawPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    candidates.Add(dir.TrimEnd('\\'));
                }
            }
        }

        if (string.IsNullOrEmpty(regDisplayVersion))
        {
            string dispVer = key.GetValue("DisplayVersion") as string ?? string.Empty;
            if (!string.IsNullOrEmpty(dispVer))
            {
                regDisplayVersion = dispVer;
            }
        }
    }
}
