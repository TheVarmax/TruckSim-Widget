using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Compatibility;
using TruckSimWidgetSetup.Diagnostics;
using TruckSimWidgetSetup.FileManager;
using TruckSimWidgetSetup.GameDiscovery;
using TruckSimWidgetSetup.InstallationState;
using TruckSimWidgetSetup.PluginManager;
using TruckSimWidgetSetup.TransactionEngine;

namespace TruckSimWidgetSetup.InstallerCore;

public class InstallerService
{
    public static async Task<bool> ExecuteInstallOrUpdateAsync(
        InstallOptions options,
        InstallationInfo installInfo,
        IProgress<double>? progress = null,
        IProgress<string>? statusText = null)
    {
        string targetDir = !string.IsNullOrEmpty(options.CustomInstallDir)
            ? options.CustomInstallDir
            : (!string.IsNullOrEmpty(installInfo.InstallPath) ? installInfo.InstallPath : Constants.GetDefaultAppDir());

        string opName = installInfo.IsInstalled
            ? (options.IsReinstallMode ? "Reinstall" : "Update")
            : "Install";

        statusText?.Report($"Preparing {opName}...");
        InstallerLogger.LogInfo($"Beginning {opName} into: {targetDir}");

        // Wait or ensure main app process is closed
        EnsureAppProcessClosed();

        // 1. Initialize payload provider
        using var payload = new EmbeddedPayloadProvider(options.CustomSourceDir, options.CustomZipPath);
        var currentManifest = payload.Manifest;

        // Initialize ElevatedHelper runner with expected hash from manifest if present
        if (currentManifest.TryGetEntry("ElevatedHelper.exe", out var helperEntry) && helperEntry != null)
        {
            string helperPath = Path.Combine(targetDir, "ElevatedHelper.exe");
            ElevatedHelperRunner.Initialize(helperPath, helperEntry.Sha256);
        }

        // 2. Initialize transaction journal
        var journal = TransactionJournal.StartNew(opName, currentManifest.Version);

        try
        {
            // 3. Load previous manifest if available
            PackageManifest? previousManifest = null;
            string installedManifestPath = Constants.GetInstalledManifestFilePath();
            if (File.Exists(installedManifestPath))
            {
                try
                {
                    previousManifest = PackageManifest.LoadFromFile(installedManifestPath);
                }
                catch { }
            }

            // If legacy Inno takeover and no previous manifest exists
            bool isLegacyTakeover = installInfo.IsLegacyInno;

            // 4. Plan synchronization
            statusText?.Report("Analyzing files...");
            var syncPlan = FileSynchronizer.PlanSynchronization(
                targetDir,
                currentManifest,
                previousManifest,
                isLegacyTakeover);

            // 5. Execute file synchronization
            statusText?.Report("Installing application files...");
            var installedRecords = await Task.Run(() =>
                FileSynchronizer.ExecuteSynchronization(targetDir, payload, syncPlan, journal, progress));

            // 6. If legacy Inno takeover, safely purge unins000.* artifacts
            if (isLegacyTakeover)
            {
                InnoTakeoverManager.PurgeLegacyInnoArtifacts(targetDir, journal);
            }

            // 7. Copy installer itself into app directory as uninstaller (if not running from there)
            CopySelfToAppDir(targetDir, journal);

            // 8. Configure telemetry plugins
            statusText?.Report("Configuring telemetry plugins...");
            if (options.Ets2Config.UserSelected && !string.IsNullOrEmpty(options.Ets2Config.SelectedPath))
            {
                TelemetryPluginManager.InstallPluginForGame(options.Ets2Config, payload, journal);
            }

            if (options.AtsConfig.UserSelected && !string.IsNullOrEmpty(options.AtsConfig.SelectedPath))
            {
                TelemetryPluginManager.InstallPluginForGame(options.AtsConfig, payload, journal);
            }

            // 9. Save canonical v3 install state
            statusText?.Report("Saving installation state...");
            var state = installInfo.ExistingState ?? new InstallStateModel();
            state.SchemaVersion = 3;
            state.InstallerVersion = currentManifest.Version;
            state.LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            state.InstallPath = targetDir;
            state.InstalledFiles = installedRecords;

            // Update game states
            if (options.Ets2Config.UserSelected && !string.IsNullOrEmpty(options.Ets2Config.SelectedPath))
            {
                state.Games[Constants.GameIdEts2] = new GameInstallState
                {
                    GameId = Constants.GameIdEts2,
                    Configured = true,
                    GamePath = options.Ets2Config.SelectedPath,
                    PluginPath = options.Ets2Config.TargetPluginFile,
                    OwnershipStatus = options.Ets2Config.OwnershipStatus,
                    InstalledPluginHash = options.Ets2Config.ExistingFileHash,
                    BackupPath = options.Ets2Config.BackupPath
                };
            }

            if (options.AtsConfig.UserSelected && !string.IsNullOrEmpty(options.AtsConfig.SelectedPath))
            {
                state.Games[Constants.GameIdAts] = new GameInstallState
                {
                    GameId = Constants.GameIdAts,
                    Configured = true,
                    GamePath = options.AtsConfig.SelectedPath,
                    PluginPath = options.AtsConfig.TargetPluginFile,
                    OwnershipStatus = options.AtsConfig.OwnershipStatus,
                    InstalledPluginHash = options.AtsConfig.ExistingFileHash,
                    BackupPath = options.AtsConfig.BackupPath
                };
            }

            string stateFilePath = Constants.GetInstallStateFilePath();
            string? stateDir = Path.GetDirectoryName(stateFilePath);
            if (!string.IsNullOrEmpty(stateDir) && !Directory.Exists(stateDir))
            {
                Directory.CreateDirectory(stateDir);
            }

            string stateBackup = Path.Combine(journal.StagingDir, "state_backup.json");
            if (File.Exists(stateFilePath))
            {
                File.Copy(stateFilePath, stateBackup, overwrite: true);
            }

            int stateStep = journal.BeginStep("SaveStateFile", string.Empty, stateFilePath, stateBackup, string.Empty);
            File.WriteAllText(stateFilePath, state.ToJson());
            journal.CompleteStep(stateStep);

            // Save installed manifest for fast deterministic comparison on next update
            currentManifest.SaveToFile(installedManifestPath);

            // 10. Register in Windows Uninstall registry
            WindowsRegistration.Register(targetDir, currentManifest.Version);

            // 11. Create Shortcuts
            ShellHelper.CreateAppShortcuts(targetDir, options.CreateDesktopShortcut);

            // 12. Commit transaction!
            journal.Commit();
            InstallerLogger.LogInfo($"{opName} completed successfully.");
            statusText?.Report("Completed!");

            // 13. Launch application if requested
            if (options.LaunchAppAfter)
            {
                LaunchInstalledApp(targetDir, isUpdate: options.IsUpdateMode);
            }

            return true;
        }
        catch (Exception ex)
        {
            InstallerLogger.LogErr($"{opName} failed with error: {ex.Message}\n{ex.StackTrace}");
            statusText?.Report($"Error: {ex.Message}. Rolling back changes...");
            journal.Rollback();
            return false;
        }
    }

    public static async Task<bool> ExecuteUninstallAsync(
        bool removeUserData,
        IProgress<double>? progress = null,
        IProgress<string>? statusText = null)
    {
        statusText?.Report("Preparing uninstallation...");
        InstallerLogger.LogInfo($"Beginning uninstallation (RemoveUserData: {removeUserData})");

        // Ensure main app is closed
        EnsureAppProcessClosed();

        var installInfo = InstallationDetector.Detect();
        string appDir = !string.IsNullOrEmpty(installInfo.InstallPath)
            ? installInfo.InstallPath
            : Constants.GetDefaultAppDir();

        var journal = TransactionJournal.StartNew("Uninstall", installInfo.InstalledVersion);

        try
        {
            // 1. Process telemetry plugins uninstall
            statusText?.Report("Restoring telemetry plugins...");
            if (installInfo.ExistingState != null)
            {
                foreach (var kvp in installInfo.ExistingState.Games)
                {
                    TelemetryPluginManager.ProcessGamePluginUninstall(kvp.Value);
                }
            }

            // 2. Remove installer-owned application files
            statusText?.Report("Removing application files...");
            var ownedFiles = new List<string>();

            // Collect owned files from installed-manifest or install-state
            string installedManifestPath = Constants.GetInstalledManifestFilePath();
            if (File.Exists(installedManifestPath))
            {
                try
                {
                    var m = PackageManifest.LoadFromFile(installedManifestPath);
                    ownedFiles.AddRange(m.Files.Select(f => Path.Combine(appDir, f.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
                }
                catch { }
            }

            if (installInfo.ExistingState != null && installInfo.ExistingState.InstalledFiles.Count > 0)
            {
                foreach (var f in installInfo.ExistingState.InstalledFiles)
                {
                    string full = Path.Combine(appDir, f.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                    if (!ownedFiles.Contains(full, StringComparer.OrdinalIgnoreCase))
                    {
                        ownedFiles.Add(full);
                    }
                }
            }

            // Fallback for legacy Inno files if manifests were missing
            if (ownedFiles.Count == 0 && Directory.Exists(appDir))
            {
                foreach (var file in Directory.GetFiles(appDir, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(appDir, file);
                    if (KnownLegacyFiles.IsLegacyInnoServiceFile(rel) || KnownLegacyFiles.IsLegacyUnambiguousAppBinary(file))
                    {
                        ownedFiles.Add(file);
                    }
                }
            }

            // Delete each owned file. NEVER delete unknown/user files!
            for (int i = 0; i < ownedFiles.Count; i++)
            {
                string file = ownedFiles[i];
                if (File.Exists(file))
                {
                    try
                    {
                        File.Delete(file);
                        InstallerLogger.LogInfo($"Deleted owned file during uninstall: {file}");
                    }
                    catch (Exception ex)
                    {
                        InstallerLogger.LogWarn($"Could not delete {file}: {ex.Message}");
                    }
                }
                progress?.Report((double)(i + 1) / Math.Max(1, ownedFiles.Count));
            }

            // 3. Remove shortcuts
            statusText?.Report("Removing shortcuts...");
            ShellHelper.RemoveAppShortcuts();

            // 4. Remove Windows Uninstall registration
            statusText?.Report("Removing Windows registration...");
            WindowsRegistration.Unregister();

            // 5. Clean only empty directories in appDir
            DirectoryCleaner.CleanEmptyDirectories(appDir);

            // 6. User data handling
            if (removeUserData)
            {
                statusText?.Report("Removing user data...");
                string userDataDir = Constants.GetUserDataDir();
                if (Directory.Exists(userDataDir))
                {
                    try
                    {
                        Directory.Delete(userDataDir, recursive: true);
                        InstallerLogger.LogInfo($"Deleted user data directory: {userDataDir}");
                    }
                    catch (Exception ex)
                    {
                        InstallerLogger.LogWarn($"Could not delete user data directory {userDataDir}: {ex.Message}");
                    }
                }

                // Delete settings registry key
                try
                {
                    using var root = Registry.CurrentUser.OpenSubKey(@"Software", writable: true);
                    root?.DeleteSubKeyTree("TruckSim Widget", throwOnMissingSubKey: false);
                    InstallerLogger.LogInfo("Deleted registry key Software\\TruckSim Widget");
                }
                catch { }
            }
            else
            {
                InstallerLogger.LogInfo($"User data preserved at: {Constants.GetUserDataDir()}");
            }

            // 7. Commit transaction
            journal.Commit();
            InstallerLogger.LogInfo("Uninstallation completed successfully.");
            statusText?.Report("Uninstall completed!");

            // 8. Self-delete schedule if running from app directory
            ScheduleSelfDeleteIfInAppDir(appDir);

            return true;
        }
        catch (Exception ex)
        {
            InstallerLogger.LogErr($"Uninstall failed: {ex.Message}\n{ex.StackTrace}");
            statusText?.Report($"Uninstall failed: {ex.Message}");
            journal.Rollback();
            return false;
        }
    }

    private static void EnsureAppProcessClosed()
    {
        try
        {
            var procs = Process.GetProcessesByName("TruckSim Widget");
            foreach (var p in procs)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(3000);
                }
                catch { }
            }
        }
        catch { }
    }

    private static void CopySelfToAppDir(string targetDir, TransactionJournal journal)
    {
        try
        {
            string currentExe = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory;
            string targetExe = Path.Combine(targetDir, Constants.InstallerExeName);

            if (string.Equals(Path.GetFullPath(currentExe), Path.GetFullPath(targetExe), StringComparison.OrdinalIgnoreCase))
            {
                // Running from target itself, no copy needed
                return;
            }

            if (File.Exists(currentExe))
            {
                int step = journal.BeginStep("CopyFile", currentExe, targetExe, string.Empty, string.Empty);
                File.Copy(currentExe, targetExe, overwrite: true);
                journal.CompleteStep(step);
                InstallerLogger.LogInfo($"Copied installer to: {targetExe}");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Could not copy self to application directory: {ex.Message}");
        }
    }

    private static void ScheduleSelfDeleteIfInAppDir(string appDir)
    {
        try
        {
            string currentExe = Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe)) return;

            string normCurrent = Path.GetFullPath(currentExe);
            string normApp = Path.GetFullPath(appDir);

            if (normCurrent.StartsWith(normApp, StringComparison.OrdinalIgnoreCase))
            {
                // Run cmd in background to delete exe after exit
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C choice /C Y /N /D Y /T 2 & del \"{normCurrent}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });
            }
        }
        catch { }
    }

    private static void LaunchInstalledApp(string appDir, bool isUpdate)
    {
        string exePath = Path.Combine(appDir, Constants.AppExeName);
        if (File.Exists(exePath))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = isUpdate ? "--updated" : string.Empty,
                    UseShellExecute = true,
                    WorkingDirectory = appDir
                };

                Process.Start(psi);
                InstallerLogger.LogInfo($"Launched installed application: {exePath} (isUpdate: {isUpdate})");
            }
            catch (Exception ex)
            {
                InstallerLogger.LogWarn($"Could not launch installed application: {ex.Message}");
            }
        }
    }
}
