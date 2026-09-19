using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;
using TruckSimWidgetSetup.FileManager;
using TruckSimWidgetSetup.GameDiscovery;
using TruckSimWidgetSetup.InstallationState;
using TruckSimWidgetSetup.TransactionEngine;

namespace TruckSimWidgetSetup.PluginManager;

public static class TelemetryPluginManager
{
    public static void EvaluatePluginOwnership(GameConfig game, string bundledPluginHash)
    {
        if (string.IsNullOrEmpty(game.SelectedPath))
        {
            game.OwnershipStatus = PluginOwnershipStatus.None;
            return;
        }

        string pluginFile = game.TargetPluginFile;
        game.ExistingFileFound = File.Exists(pluginFile);

        if (!game.ExistingFileFound)
        {
            game.OwnershipStatus = PluginOwnershipStatus.None;
            game.ExistingFileHash = string.Empty;
            InstallerLogger.LogInfo($"[{game.GameId}] No existing telemetry plugin found at: {pluginFile}");
            return;
        }

        string recordedHash = game.ExistingFileHash;
        string currentHash = PackageManifest.ComputeFileSha256(pluginFile);
        InstallerLogger.LogInfo($"[{game.GameId}] Existing telemetry plugin found. SHA-256: {currentHash}, Recorded: {recordedHash}");

        // If already recorded as Owned in install state
        if (string.Equals(game.OwnershipStatus, PluginOwnershipStatus.Owned, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(recordedHash) &&
                string.Equals(currentHash, recordedHash, StringComparison.OrdinalIgnoreCase))
            {
                InstallerLogger.LogInfo($"[{game.GameId}] Plugin is verified owned and unmodified.");
                game.OwnershipStatus = PluginOwnershipStatus.Owned;
            }
            else
            {
                InstallerLogger.LogWarn($"[{game.GameId}] Plugin was owned by Widget but has been modified since install.");
                game.OwnershipStatus = PluginOwnershipStatus.ModifiedByUser;
            }
            game.ExistingFileHash = currentHash;
            return;
        }

        if (string.Equals(game.OwnershipStatus, PluginOwnershipStatus.LegacyOwnedUnverified, StringComparison.OrdinalIgnoreCase))
        {
            InstallerLogger.LogWarn($"[{game.GameId}] Legacy unverified plugin ownership preserved.");
            game.ExistingFileHash = currentHash;
            return;
        }

        // STRICT RULE: matching hash alone != ownership
        if (string.Equals(currentHash, bundledPluginHash, StringComparison.OrdinalIgnoreCase))
        {
            InstallerLogger.LogInfo($"[{game.GameId}] Plugin matches bundled hash but has no ownership record (unmanaged/third-party).");
        }
        else
        {
            InstallerLogger.LogInfo($"[{game.GameId}] Third-party plugin detected with different hash.");
        }

        game.OwnershipStatus = PluginOwnershipStatus.None;
        game.ExistingFileHash = currentHash;
    }

    public static bool InstallPluginForGame(
        GameConfig game,
        EmbeddedPayloadProvider payloadProvider,
        TransactionJournal journal)
    {
        if (!game.UserSelected || string.IsNullOrEmpty(game.SelectedPath))
        {
            InstallerLogger.LogInfo($"[{game.GameId}] Plugin installation skipped by user preference.");
            game.OwnershipStatus = PluginOwnershipStatus.Skipped;
            return true;
        }

        string targetDir = Path.Combine(game.SelectedPath, Constants.GameRelPluginDir);
        string targetFile = game.TargetPluginFile;

        InstallerLogger.LogInfo($"[{game.GameId}] Installing telemetry plugin to: {targetFile}");

        // 1. Ensure directory exists
        if (!Directory.Exists(targetDir))
        {
            int dirStep = journal.BeginStep("CreateDir", string.Empty, targetDir, string.Empty, string.Empty);
            try
            {
                Directory.CreateDirectory(targetDir);
            }
            catch
            {
                if (!ElevatedHelperRunner.MkDirElevated(targetDir))
                {
                    InstallerLogger.LogErr($"[{game.GameId}] Could not create plugins directory: {targetDir}");
                    return false;
                }
            }
            journal.CompleteStep(dirStep);
        }

        string stagingBackupFile = string.Empty;
        string currentHash = string.Empty;

        // 2. Handle existing file
        if (File.Exists(targetFile))
        {
            currentHash = PackageManifest.ComputeFileSha256(targetFile);

            if (game.ConflictAction == PluginConflictAction.KeepExisting)
            {
                InstallerLogger.LogInfo($"[{game.GameId}] User chose to keep existing plugin.");
                game.OwnershipStatus = PluginOwnershipStatus.Skipped;
                return true;
            }

            // Always create temporary staging rollback backup
            string stagingDir = journal.StagingDir;
            if (!Directory.Exists(stagingDir)) Directory.CreateDirectory(stagingDir);
            stagingBackupFile = Path.Combine(stagingDir, $"{game.GameId}_rollback_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dll");

            int stageStep = journal.BeginStep("StageRollbackBackup", targetFile, targetFile, stagingBackupFile, currentHash);
            try
            {
                File.Copy(targetFile, stagingBackupFile, overwrite: true);
            }
            catch
            {
                if (!ElevatedHelperRunner.CopyFileElevated(targetFile, stagingBackupFile))
                {
                    InstallerLogger.LogErr($"[{game.GameId}] Failed to create temporary staging rollback backup.");
                    return false;
                }
            }
            journal.CompleteStep(stageStep);

            // Create persistent backup ONLY if user chose BackupReplace
            if (game.ConflictAction == PluginConflictAction.BackupReplace)
            {
                string backupTarget = Path.Combine(targetDir, Constants.PluginFileName + Constants.PluginBackupExt);
                if (File.Exists(backupTarget))
                {
                    backupTarget = Path.Combine(targetDir, $"{Constants.PluginFileName}.backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak");
                }

                InstallerLogger.LogInfo($"[{game.GameId}] Creating persistent backup of third-party plugin: {backupTarget}");
                int backupStep = journal.BeginStep("CreateThirdPartyBackup", targetFile, targetFile, backupTarget, currentHash);
                try
                {
                    File.Copy(targetFile, backupTarget, overwrite: true);
                }
                catch
                {
                    if (!ElevatedHelperRunner.CopyFileElevated(targetFile, backupTarget))
                    {
                        InstallerLogger.LogErr($"[{game.GameId}] Failed to create third-party plugin backup.");
                        return false;
                    }
                }
                journal.CompleteStep(backupStep);

                game.BackupPath = backupTarget;
                game.BackupHash = currentHash;
            }
            else
            {
                game.BackupPath = string.Empty;
                game.BackupHash = string.Empty;
            }
        }

        // 3. Extract and write bundled plugin
        using var pluginStream = payloadProvider.OpenTelemetryPluginStream();
        if (pluginStream == null)
        {
            InstallerLogger.LogErr($"[{game.GameId}] Bundled scs-telemetry.dll payload stream not found!");
            return false;
        }

        // Write to temporary staging file first
        string tempStagedPlugin = Path.Combine(journal.StagingDir, $"{game.GameId}_new_plugin.dll");
        using (var fs = File.Create(tempStagedPlugin))
        {
            pluginStream.CopyTo(fs);
        }

        string rollbackRef = !string.IsNullOrEmpty(stagingBackupFile) ? stagingBackupFile : game.BackupPath;
        int copyStep = journal.BeginStep("CopyPlugin", tempStagedPlugin, targetFile, rollbackRef, currentHash);

        bool copied = false;
        try
        {
            File.Copy(tempStagedPlugin, targetFile, overwrite: true);
            copied = true;
        }
        catch
        {
            InstallerLogger.LogWarn($"[{game.GameId}] Standard Copy failed. Attempting elevated copy.");
            copied = ElevatedHelperRunner.CopyFileElevated(tempStagedPlugin, targetFile);
        }

        if (copied && File.Exists(targetFile))
        {
            string installedHash = PackageManifest.ComputeFileSha256(targetFile);
            journal.CompleteStep(copyStep, installedHash);

            game.ExistingFileHash = installedHash;
            game.OwnershipStatus = game.ConflictAction == PluginConflictAction.BackupReplace
                ? PluginOwnershipStatus.ThirdPartyReplaced
                : PluginOwnershipStatus.Owned;

            InstallerLogger.LogInfo($"[{game.GameId}] Telemetry plugin installed successfully. Status: {game.OwnershipStatus}, Hash: {installedHash}");
            return true;
        }
        else
        {
            InstallerLogger.LogErr($"[{game.GameId}] Failed to copy plugin to: {targetFile}");
            return false;
        }
    }

    public static void ProcessGamePluginUninstall(GameInstallState state)
    {
        string targetFile = state.PluginPath;
        if (!File.Exists(targetFile))
        {
            InstallerLogger.LogInfo($"[{state.GameId}] Plugin file does not exist at uninstall: {targetFile}");
            return;
        }

        string currentHash = PackageManifest.ComputeFileSha256(targetFile);
        InstallerLogger.LogInfo($"[{state.GameId}] Checking plugin for uninstall. Current: {currentHash}, Recorded: {state.InstalledPluginHash}");

        // Check if modified by user
        if (!string.IsNullOrEmpty(state.InstalledPluginHash) &&
            !string.Equals(currentHash, state.InstalledPluginHash, StringComparison.OrdinalIgnoreCase))
        {
            InstallerLogger.LogWarn($"[{state.GameId}] Plugin file was modified after install! Preserving file untouched.");
            return;
        }

        // Case: Third-party plugin was backed up and replaced -> Restore original
        if (string.Equals(state.OwnershipStatus, PluginOwnershipStatus.ThirdPartyReplaced, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(state.BackupPath))
        {
            InstallerLogger.LogInfo($"[{state.GameId}] Restoring original third-party plugin from: {state.BackupPath}");
            try
            {
                File.Copy(state.BackupPath, targetFile, overwrite: true);
                File.Delete(state.BackupPath);
                InstallerLogger.LogInfo($"[{state.GameId}] Successfully restored original third-party plugin.");
            }
            catch
            {
                if (ElevatedHelperRunner.CopyFileElevated(state.BackupPath, targetFile))
                {
                    ElevatedHelperRunner.DeleteFileElevated(state.BackupPath);
                    InstallerLogger.LogInfo($"[{state.GameId}] Successfully restored original third-party plugin elevated.");
                }
                else
                {
                    InstallerLogger.LogErr($"[{state.GameId}] Failed to restore original plugin backup.");
                }
            }
            return;
        }

        // Case: Clean Widget-owned plugin -> Delete
        if (string.Equals(state.OwnershipStatus, PluginOwnershipStatus.Owned, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state.OwnershipStatus, PluginOwnershipStatus.LegacyOwnedUnverified, StringComparison.OrdinalIgnoreCase))
        {
            InstallerLogger.LogInfo($"[{state.GameId}] Removing Widget-owned plugin: {targetFile}");
            try
            {
                File.Delete(targetFile);
                InstallerLogger.LogInfo($"[{state.GameId}] Plugin successfully removed.");
            }
            catch
            {
                if (ElevatedHelperRunner.DeleteFileElevated(targetFile))
                {
                    InstallerLogger.LogInfo($"[{state.GameId}] Plugin successfully removed elevated.");
                }
                else
                {
                    InstallerLogger.LogWarn($"[{state.GameId}] Could not delete plugin file.");
                }
            }
        }
    }
}
