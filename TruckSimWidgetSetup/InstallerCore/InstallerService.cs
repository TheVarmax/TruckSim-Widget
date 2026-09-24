using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using TruckSimWidgetSetup.Common;
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

        TransactionJournal? journal = null;
        try
        {
            if (!CrashRecoveryEngine.CheckAndExecuteRecovery())
                throw new InvalidOperationException("A previous installation could not be recovered.");

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
            journal = TransactionJournal.StartNew(opName, currentManifest.Version);
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

            // 4. Plan synchronization
            statusText?.Report("Analyzing files...");
            var syncPlan = FileSynchronizer.PlanSynchronization(
                targetDir,
                currentManifest,
                previousManifest);

            // 5. Execute file synchronization
            statusText?.Report("Installing application files...");
            var installedRecords = await Task.Run(() =>
                FileSynchronizer.ExecuteSynchronization(targetDir, payload, syncPlan, journal, progress));

            // 6. Install or update installer itself in app directory as an explicitly owned binary
            statusText?.Report("Installing installer executable...");
            InstallOrUpdateInstallerExe(targetDir, installInfo, journal, installedRecords);

            // 7. Configure telemetry plugins
            statusText?.Report("Configuring telemetry plugins...");
            if (options.Ets2Config.UserSelected && !string.IsNullOrEmpty(options.Ets2Config.SelectedPath))
            {
                bool success = TelemetryPluginManager.InstallPluginForGame(options.Ets2Config, payload, journal, options.IsUpdateMode);
                if (!success)
                {
                    throw new InvalidOperationException("Failed to install telemetry plugin for ETS2.");
                }
            }

            if (options.AtsConfig.UserSelected && !string.IsNullOrEmpty(options.AtsConfig.SelectedPath))
            {
                bool success = TelemetryPluginManager.InstallPluginForGame(options.AtsConfig, payload, journal, options.IsUpdateMode);
                if (!success)
                {
                    throw new InvalidOperationException("Failed to install telemetry plugin for ATS.");
                }
            }

            // 8. Save canonical v3 install state
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
            string originalStateHash = string.Empty;
            if (File.Exists(stateFilePath))
            {
                File.Copy(stateFilePath, stateBackup, overwrite: true);
                originalStateHash = PackageManifest.ComputeFileSha256(stateFilePath);
            }

            int stateStep = journal.BeginStep("SaveStateFile", string.Empty, stateFilePath, stateBackup, originalStateHash);
            File.WriteAllText(stateFilePath, state.ToJson());
            journal.CompleteStep(stateStep, PackageManifest.ComputeFileSha256(stateFilePath));

            // Save installed manifest for fast deterministic comparison on next update (TRANSACTIONAL)
            installedManifestPath = Constants.GetInstalledManifestFilePath();
            string? manifestDir = Path.GetDirectoryName(installedManifestPath);
            if (!string.IsNullOrEmpty(manifestDir) && !Directory.Exists(manifestDir))
            {
                Directory.CreateDirectory(manifestDir);
            }

            string manifestBackup = Path.Combine(journal.StagingDir, "manifest_backup.json");
            string originalManifestHash = string.Empty;
            if (File.Exists(installedManifestPath))
            {
                File.Copy(installedManifestPath, manifestBackup, overwrite: true);
                originalManifestHash = PackageManifest.ComputeFileSha256(installedManifestPath);
            }

            int manifestStep = journal.BeginStep("SaveManifestFile", string.Empty, installedManifestPath, manifestBackup, originalManifestHash);
            currentManifest.SaveToFile(installedManifestPath);
            journal.CompleteStep(manifestStep, PackageManifest.ComputeFileSha256(installedManifestPath));

            // 9. Register in Windows Uninstall registry (TRANSACTIONAL)
            statusText?.Report("Registering application...");
            int regStep = journal.BeginStep("RegisterWindowsUninstall", string.Empty, Constants.UninstallRegSubKey, string.Empty, string.Empty);
            bool registered = WindowsRegistration.Register(targetDir, currentManifest.Version);
            if (!registered)
            {
                throw new InvalidOperationException("Failed to register application in Windows Uninstall registry.");
            }
            journal.CompleteStep(regStep);

            // 10. Create Shortcuts
            ShellHelper.CreateAppShortcuts(targetDir, options.CreateDesktopShortcut);

            // 11. Commit transaction!
            journal.Commit();
            InstallerLogger.LogInfo($"{opName} completed successfully.");
            statusText?.Report("Completed!");

            // 12. Launch application only in headless silent mode
            // (In GUI mode, ViewModel/MainWindow handles launching once upon completion)
            if (options.IsSilent && options.LaunchAppAfter)
            {
                LaunchInstalledApp(targetDir, isUpdate: options.IsUpdateMode);
            }

            return true;
        }
        catch (Exception ex)
        {
            InstallerLogger.LogErr($"{opName} failed with error: {ex.Message}\n{ex.StackTrace}");
            statusText?.Report($"Error: {ex.Message}. Rolling back changes...");
            if (journal != null)
            {
                try { journal.Rollback(); }
                catch (Exception rollbackEx) { InstallerLogger.LogErr($"Rollback could not finish: {rollbackEx.Message}"); }
            }
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

        TransactionJournal? journal = null;
        try
        {
            if (!CrashRecoveryEngine.CheckAndExecuteRecovery())
                throw new InvalidOperationException("A previous installation could not be recovered.");

            // Ensure main app is closed
            EnsureAppProcessClosed();

            var installInfo = InstallationDetector.Detect();
            string appDir = !string.IsNullOrEmpty(installInfo.InstallPath)
                ? installInfo.InstallPath
                : Constants.GetDefaultAppDir();

            journal = TransactionJournal.StartNew("Uninstall", installInfo.InstalledVersion);
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

            // 6. Commit transaction before destructive user data deletion
            journal.Commit();
            InstallerLogger.LogInfo("Uninstallation transaction committed successfully.");

            // 7. User data handling (AFTER transaction commit)
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

            statusText?.Report("Uninstall completed!");

            // 8. Self-delete schedule if running from app directory and verified owned
            bool isSelfOwned = !string.IsNullOrEmpty(Environment.ProcessPath) &&
                ownedFiles.Contains(Path.GetFullPath(Environment.ProcessPath), StringComparer.OrdinalIgnoreCase);
            ScheduleSelfDeleteIfInAppDir(appDir, isSelfOwned);

            return true;
        }
        catch (Exception ex)
        {
            InstallerLogger.LogErr($"Uninstall failed: {ex.Message}\n{ex.StackTrace}");
            statusText?.Report($"Uninstall failed: {ex.Message}");
            if (journal != null)
            {
                try { journal.Rollback(); }
                catch (Exception rollbackEx) { InstallerLogger.LogErr($"Rollback could not finish: {rollbackEx.Message}"); }
            }
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

    private static void InstallOrUpdateInstallerExe(
        string targetDir,
        InstallationInfo installInfo,
        TransactionJournal journal,
        List<InstalledFileRecord> installedRecords)
    {
        string currentExe = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory;
        string targetExe = Path.Combine(targetDir, Constants.InstallerExeName);

        // If running directly from destination, already in place
        if (File.Exists(targetExe) &&
            string.Equals(Path.GetFullPath(currentExe), Path.GetFullPath(targetExe), StringComparison.OrdinalIgnoreCase))
        {
            string hash = PackageManifest.ComputeFileSha256(targetExe);
            installedRecords.Add(new InstalledFileRecord
            {
                RelativePath = Constants.InstallerExeName,
                Sha256 = hash
            });
            InstallerLogger.LogInfo($"Installer is already running from application directory: {targetExe}");
            return;
        }

        if (!File.Exists(currentExe))
        {
            throw new FileNotFoundException($"Source installer executable not found at: {currentExe}");
        }

        string stagingDir = journal.StagingDir;
        if (!Directory.Exists(stagingDir)) Directory.CreateDirectory(stagingDir);

        if (File.Exists(targetExe))
        {
            // STRICT RULE: If target exists, verify ownership before overwriting!
            bool isOwned = OwnershipManager.IsInstallerExeOwned(targetExe, installInfo);
            if (!isOwned)
            {
                InstallerLogger.LogErr($"Target installer executable '{targetExe}' exists and is NOT owned by installer!");
                throw new InvalidOperationException(
                    $"Cannot install '{Constants.InstallerExeName}' because an unowned file already exists at '{targetExe}'. Existing user/unknown files cannot be overwritten.");
            }

            string stagingBackup = Path.Combine(stagingDir, $"replace_installer_{Guid.NewGuid():N}.bak");
            File.Copy(targetExe, stagingBackup, overwrite: true);
            string originalHash = PackageManifest.ComputeFileSha256(targetExe);

            int step = journal.BeginStep("ReplaceFile", currentExe, targetExe, stagingBackup, originalHash);
            File.Copy(currentExe, targetExe, overwrite: true);

            string newHash = PackageManifest.ComputeFileSha256(targetExe);
            journal.CompleteStep(step, newHash);

            installedRecords.Add(new InstalledFileRecord
            {
                RelativePath = Constants.InstallerExeName,
                Sha256 = newHash
            });

            InstallerLogger.LogInfo($"Updated owned installer executable: {targetExe} (SHA-256: {newHash})");
        }
        else
        {
            int step = journal.BeginStep("CopyFile", currentExe, targetExe, string.Empty, string.Empty);
            File.Copy(currentExe, targetExe, overwrite: false);

            string newHash = PackageManifest.ComputeFileSha256(targetExe);
            journal.CompleteStep(step, newHash);

            installedRecords.Add(new InstalledFileRecord
            {
                RelativePath = Constants.InstallerExeName,
                Sha256 = newHash
            });

            InstallerLogger.LogInfo($"Installed owned installer executable: {targetExe} (SHA-256: {newHash})");
        }
    }

    private static void ScheduleSelfDeleteIfInAppDir(string appDir, bool isOwned)
    {
        if (!isOwned) return;
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

    public static void LaunchInstalledApp(string appDir, bool isUpdate)
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
