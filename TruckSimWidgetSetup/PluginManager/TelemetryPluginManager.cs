using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;
using TruckSimWidgetSetup.FileManager;
using TruckSimWidgetSetup.GameDiscovery;
using TruckSimWidgetSetup.InstallationState;
using TruckSimWidgetSetup.TransactionEngine;
using System.Security.Cryptography;

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
        TransactionJournal journal,
        bool isUpdateMode = false)
        => InstallPluginForGame(game, payloadProvider, journal, isUpdateMode, new PluginFileOperations());

    internal static bool InstallPluginForGame(
        GameConfig game, EmbeddedPayloadProvider payloadProvider, TransactionJournal journal,
        bool isUpdateMode, PluginFileOperations files)
    {
        if (!game.UserSelected || string.IsNullOrEmpty(game.SelectedPath))
        {
            InstallerLogger.LogInfo($"[{game.GameId}] Plugin installation skipped by user preference.");
            game.OwnershipStatus = PluginOwnershipStatus.Skipped;
            return true;
        }

        if (isUpdateMode && game.ConflictAction == PluginConflictAction.None)
        {
            game.ConflictAction = PluginConflictAction.Overwrite;
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
            string stagingDir = journal.GetPluginStagingDir();
            stagingBackupFile = Path.Combine(stagingDir, $"{game.GameId}_rollback_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dll");

            int stageStep = journal.BeginStep("StageRollbackBackup", targetFile, targetFile, stagingBackupFile, currentHash);
            try
            {
                PluginFileOperations.StageVerified(targetFile, stagingBackupFile, currentHash);
            }
            catch
            {
                // TEMP is intentionally source-only in ElevatedHelper. Failure
                // to read the original safely must not proceed with replacement.
                InstallerLogger.LogErr($"[{game.GameId}] Failed to verify and stage rollback backup.");
                return false;
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
                if (files.CopyVerified(stagingBackupFile, backupTarget, currentHash) != PluginCopyResult.Success)
                {
                    InstallerLogger.LogErr($"[{game.GameId}] Failed to create verified third-party plugin backup.");
                    return false;
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

        // The application manifest deliberately excludes the telemetry DLL.
        // Its trusted digest comes from the bundled payload stream, never staging.
        string expectedPluginHash = Convert.ToHexString(SHA256.HashData(pluginStream));
        using var extractionStream = payloadProvider.OpenTelemetryPluginStream();
        if (extractionStream == null) return false;
        string tempStagedPlugin = Path.Combine(journal.GetPluginStagingDir(), $"{game.GameId}_new_plugin.dll");
        using (var fs = new FileStream(tempStagedPlugin, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            extractionStream.CopyTo(fs);
            fs.Flush(flushToDisk: true);
        }

        string rollbackRef = !string.IsNullOrEmpty(stagingBackupFile) ? stagingBackupFile : game.BackupPath;
        // A running game's original DLL may legitimately be in use. Skip only
        // before mutation; a locked/unreadable destination after copy is failure.
        if (IsFileLocked(targetFile))
        {
            InstallerLogger.LogWarn($"[{game.GameId}] Original plugin is in use; skipping overwrite.");
            return true;
        }
        int copyStep = journal.BeginStep("CopyPlugin", tempStagedPlugin, targetFile, rollbackRef, currentHash);

        PluginCopyResult copied = files.CopyVerified(tempStagedPlugin, targetFile, expectedPluginHash);

        if (copied == PluginCopyResult.Success)
        {
            string installedHash = expectedPluginHash.ToLowerInvariant();
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

    private static bool IsFileLocked(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void ProcessGamePluginUninstall(GameInstallState state)
        => ProcessGamePluginUninstall(state, new PluginFileOperations());

    internal static void ProcessGamePluginUninstall(GameInstallState state, PluginFileOperations files)
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
            // Legacy install state stores the authorized backup path but no
            // backup digest. Snapshot the original backup before restaging;
            // revalidation and the source lease protect both copies thereafter.
            string backupHash = PackageManifest.ComputeFileSha256(state.BackupPath);
            if (!files.RestoreVerified(state.BackupPath, targetFile, backupHash))
                throw new IOException("Could not verify and restore the original telemetry plugin.");
            try { File.Delete(state.BackupPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (!ElevatedHelperRunner.DeletePluginFileElevated(state.BackupPath))
                    throw new IOException("Could not remove the restored telemetry plugin backup.");
            }
            InstallerLogger.LogInfo($"[{state.GameId}] Verified original third-party plugin restored.");
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
                if (ElevatedHelperRunner.DeletePluginFileElevated(targetFile))
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
