using Microsoft.Win32;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.InstallationState;

public static class InstallationDetector
{
    public static InstallationInfo Detect()
    {
        var info = new InstallationInfo();

        string regInstallLocation = string.Empty;
        string regDisplayVersion = string.Empty;
        string regUninstallString = string.Empty;

        bool hasRegistryEntry = false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Constants.UninstallRegSubKey);
            if (key != null)
            {
                hasRegistryEntry = true;
                regInstallLocation = key.GetValue("InstallLocation") as string ?? string.Empty;
                regDisplayVersion = key.GetValue("DisplayVersion") as string ?? string.Empty;
                regUninstallString = key.GetValue("UninstallString") as string ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Error querying uninstall registry key: {ex.Message}");
        }

        string candidateDir = !string.IsNullOrWhiteSpace(regInstallLocation)
            ? regInstallLocation.TrimEnd('\\')
            : Constants.GetDefaultAppDir();

        string expectedExe = Path.Combine(candidateDir, Constants.AppExeName);
        bool exeExists = File.Exists(expectedExe);

        // Check for existing install state file
        string stateFile = Constants.GetInstallStateFilePath();
        InstallStateModel? state = InstallStateModel.LoadFromFile(stateFile);
        info.ExistingState = state;

        if (hasRegistryEntry && exeExists)
        {
            info.InstallPath = candidateDir;
            info.InstalledVersion = !string.IsNullOrEmpty(regDisplayVersion)
                ? regDisplayVersion
                : (state?.InstallerVersion ?? "Unknown");
            info.LegacyUninstallString = regUninstallString;

            // Check if this is a legacy Inno installation
            bool isInnoUninstaller = !string.IsNullOrEmpty(regUninstallString) &&
                                     regUninstallString.Contains("unins", StringComparison.OrdinalIgnoreCase);

            if (isInnoUninstaller || state == null || state.InstalledFiles.Count == 0)
            {
                info.Status = InstallationStatus.LegacyInno;
                info.IsLegacyInno = true;
                InstallerLogger.LogInfo($"Detected Legacy Inno installation at: {candidateDir} (Version: {info.InstalledVersion})");
            }
            else
            {
                info.Status = InstallationStatus.Installed;
                info.IsLegacyInno = false;
                InstallerLogger.LogInfo($"Detected valid modern installation at: {candidateDir} (Version: {info.InstalledVersion})");
            }

            return info;
        }

        // Registry entry or exe missing. Check for stale directory.
        if (Directory.Exists(candidateDir) && Directory.EnumerateFileSystemEntries(candidateDir).Any())
        {
            info.InstallPath = candidateDir;
            info.Status = InstallationStatus.StaleDirectory;
            InstallerLogger.LogInfo($"Detected stale directory at: {candidateDir} (treating as fresh install, unknown files will be preserved).");
            return info;
        }

        info.InstallPath = candidateDir;
        info.Status = InstallationStatus.NotInstalled;
        InstallerLogger.LogInfo($"No existing installation detected. Target install path: {candidateDir}");
        return info;
    }
}
