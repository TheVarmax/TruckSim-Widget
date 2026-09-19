using Microsoft.Win32;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;
using TruckSimWidgetSetup.InstallationState;
using TruckSimWidgetSetup.TransactionEngine;

namespace TruckSimWidgetSetup.Compatibility;

public static class InnoTakeoverManager
{
    public static InstallStateModel PrepareLegacyState(string installPath, string installedVersion)
    {
        var state = new InstallStateModel
        {
            SchemaVersion = 3,
            InstallerVersion = installedVersion,
            LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            InstallPath = installPath
        };

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Constants.WidgetSettingsRegSubKey);
            if (key != null)
            {
                string? ets2Path = key.GetValue("ETS2Path") as string;
                string? ets2Plugin = key.GetValue("ETS2PluginOwnedPath") as string;
                string? atsPath = key.GetValue("ATSPath") as string;
                string? atsPlugin = key.GetValue("ATSPluginOwnedPath") as string;

                if (!string.IsNullOrEmpty(ets2Path))
                {
                    state.Games[Constants.GameIdEts2] = new GameInstallState
                    {
                        GameId = Constants.GameIdEts2,
                        Configured = true,
                        GamePath = ets2Path,
                        PluginPath = ets2Plugin ?? Path.Combine(ets2Path, Constants.GameRelPluginDir, Constants.PluginFileName),
                        OwnershipStatus = "LegacyOwnedUnverified"
                    };
                }

                if (!string.IsNullOrEmpty(atsPath))
                {
                    state.Games[Constants.GameIdAts] = new GameInstallState
                    {
                        GameId = Constants.GameIdAts,
                        Configured = true,
                        GamePath = atsPath,
                        PluginPath = atsPlugin ?? Path.Combine(atsPath, Constants.GameRelPluginDir, Constants.PluginFileName),
                        OwnershipStatus = "LegacyOwnedUnverified"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Failed to read legacy registry during takeover: {ex.Message}");
        }

        return state;
    }

    public static void PurgeLegacyInnoArtifacts(string installPath, TransactionJournal journal)
    {
        if (!Directory.Exists(installPath)) return;

        var files = Directory.GetFiles(installPath, "unins000.*");
        foreach (var file in files)
        {
            if (KnownLegacyFiles.IsLegacyInnoServiceFile(file))
            {
                try
                {
                    string backup = Path.Combine(journal.StagingDir, $"legacy_{Path.GetFileName(file)}.bak");
                    File.Copy(file, backup, overwrite: true);

                    int step = journal.BeginStep("DeleteFile", Path.GetFileName(file), file, backup, string.Empty);
                    File.Delete(file);
                    journal.CompleteStep(step);
                    InstallerLogger.LogInfo($"Safely purged legacy Inno artifact: {file}");
                }
                catch (Exception ex)
                {
                    InstallerLogger.LogWarn($"Could not remove legacy Inno file {file}: {ex.Message}");
                }
            }
        }
    }
}
