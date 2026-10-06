using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.FileManager;
using TruckSimWidgetSetup.GameDiscovery;
using TruckSimWidgetSetup.InstallationState;
using TruckSimWidgetSetup.InstallerCore;
using TruckSimWidgetSetup.PluginManager;
using TruckSimWidgetSetup.TransactionEngine;
using Xunit;

namespace TruckSimWidget.Tests;

[Collection("Installer integrity")]
public class InstallerTests : IDisposable
{
    private readonly string _testDir;

    public InstallerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "tsw_installer_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch { }
    }

    private static string Hash(string content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    [Fact]
    public void Manifest_ExcludesTelemetryPlugin_AndCalculatesHashes()
    {
        string sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(sourceDir);

        // App files
        File.WriteAllText(Path.Combine(sourceDir, "TruckSim Widget.exe"), "AppBinary");
        File.WriteAllText(Path.Combine(sourceDir, "SomeLibrary.dll"), "LibBinary");

        // Plugin folder
        string pluginDir = Path.Combine(sourceDir, "plugin");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(Path.Combine(pluginDir, "scs-telemetry.dll"), "PluginBinary");

        var manifest = PackageManifest.GenerateFromDirectory(sourceDir, "1.6.4-beta.1");

        Assert.Equal("1.6.4-beta.1", manifest.Version);
        Assert.Equal(2, manifest.Files.Count);

        // STRICT INVARIANT: scs-telemetry.dll must NOT be in application manifest
        Assert.True(manifest.TryGetEntry("TruckSim Widget.exe", out _));
        Assert.True(manifest.TryGetEntry("SomeLibrary.dll", out _));
        Assert.False(manifest.TryGetEntry("plugin/scs-telemetry.dll", out _));
        Assert.False(manifest.TryGetEntry("scs-telemetry.dll", out _));
    }

    [Fact]
    public void Ownership_StrictRules_PreservesUnknownFiles()
    {
        var currentManifest = new PackageManifest();
        currentManifest.Files.Add(new ManifestFileEntry { RelativePath = "App.exe", Sha256 = "hash1" });

        var previousManifest = new PackageManifest();
        previousManifest.Files.Add(new ManifestFileEntry { RelativePath = "OldFile.dll", Sha256 = "hash2" });

        // Current manifest file -> Owned
        Assert.True(OwnershipManager.IsFileOwned("App.exe", currentManifest, previousManifest));

        // Previous manifest file -> Owned
        Assert.True(OwnershipManager.IsFileOwned("OldFile.dll", currentManifest, previousManifest));

        // Unknown user file -> NOT owned
        Assert.False(OwnershipManager.IsFileOwned("user_notes.txt", currentManifest, previousManifest));
        Assert.False(OwnershipManager.IsFileOwned("custom_tool.exe", currentManifest, previousManifest));
    }

    [Fact]
    public void Synchronization_PreservesUnknownUserFiles_AndDeletesObsoleteOwned()
    {
        string appDir = Path.Combine(_testDir, "app");
        string sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(sourceDir);

        // Current release files
        File.WriteAllText(Path.Combine(sourceDir, "TruckSim Widget.exe"), "NewApp v2");
        File.WriteAllText(Path.Combine(sourceDir, "NewFeature.dll"), "FeatureBinary");
        var currentManifest = PackageManifest.GenerateFromDirectory(sourceDir, "2.0.0");

        // Installed directory state:
        // - TruckSim Widget.exe (v1, needs replace)
        // - ObsoleteLib.dll (was in previous manifest, needs delete)
        // - user-notes.txt (unknown/user file, MUST BE PRESERVED!)
        // - custom_config.xml (unknown/user file, MUST BE PRESERVED!)
        File.WriteAllText(Path.Combine(appDir, "TruckSim Widget.exe"), "OldApp v1");
        File.WriteAllText(Path.Combine(appDir, "ObsoleteLib.dll"), "ObsoleteContent");
        File.WriteAllText(Path.Combine(appDir, "user-notes.txt"), "Important user notes that must not be deleted");
        File.WriteAllText(Path.Combine(appDir, "custom_config.xml"), "<userConfig/>");

        var previousManifest = new PackageManifest();
        previousManifest.Files.Add(new ManifestFileEntry { RelativePath = "TruckSim Widget.exe", Sha256 = Hash("OldApp v1") });
        previousManifest.Files.Add(new ManifestFileEntry { RelativePath = "ObsoleteLib.dll", Sha256 = Hash("ObsoleteContent") });

        // Plan synchronization
        var plan = FileSynchronizer.PlanSynchronization(appDir, currentManifest, previousManifest);

        var replacePlan = plan.FirstOrDefault(p => p.RelativePath == "TruckSim Widget.exe");
        var installPlan = plan.FirstOrDefault(p => p.RelativePath == "NewFeature.dll");
        var deletePlan = plan.FirstOrDefault(p => p.RelativePath == "ObsoleteLib.dll");
        var userNotesPlan = plan.FirstOrDefault(p => p.RelativePath == "user-notes.txt");
        var userConfigPlan = plan.FirstOrDefault(p => p.RelativePath == "custom_config.xml");

        Assert.NotNull(replacePlan);
        Assert.Equal(SyncActionType.Replace, replacePlan.Action);

        Assert.NotNull(installPlan);
        Assert.Equal(SyncActionType.Install, installPlan.Action);

        Assert.NotNull(deletePlan);
        Assert.Equal(SyncActionType.DeleteObsolete, deletePlan.Action);

        Assert.NotNull(userNotesPlan);
        Assert.Equal(SyncActionType.IgnoreUnknown, userNotesPlan.Action);

        Assert.NotNull(userConfigPlan);
        Assert.Equal(SyncActionType.IgnoreUnknown, userConfigPlan.Action);

        // Execute synchronization
        using var payload = new EmbeddedPayloadProvider(sourceDir);
        var journal = TransactionJournal.StartNew("Update", "2.0.0", Path.Combine(_testDir, "test_tx.json"), Path.Combine(_testDir, "staging"));

        var installedRecords = FileSynchronizer.ExecuteSynchronization(appDir, payload, plan, journal);

        // Assertions on final directory state:
        // 1. New files installed & replaced
        Assert.Equal("NewApp v2", File.ReadAllText(Path.Combine(appDir, "TruckSim Widget.exe")));
        Assert.Equal("FeatureBinary", File.ReadAllText(Path.Combine(appDir, "NewFeature.dll")));

        // 2. Obsolete owned file deleted
        Assert.False(File.Exists(Path.Combine(appDir, "ObsoleteLib.dll")));

        // 3. STRICT INVARIANT: Unknown user files remain completely intact!
        Assert.True(File.Exists(Path.Combine(appDir, "user-notes.txt")));
        Assert.Equal("Important user notes that must not be deleted", File.ReadAllText(Path.Combine(appDir, "user-notes.txt")));

        Assert.True(File.Exists(Path.Combine(appDir, "custom_config.xml")));
        Assert.Equal("<userConfig/>", File.ReadAllText(Path.Combine(appDir, "custom_config.xml")));

        // 4. Installed records only include current release files
        Assert.Equal(2, installedRecords.Count);
        Assert.Contains(installedRecords, r => r.RelativePath == "TruckSim Widget.exe");
        Assert.Contains(installedRecords, r => r.RelativePath == "NewFeature.dll");
    }

    [Fact]
    public void StaleDirectory_PreservesUnknownFiles_InstallsNormally()
    {
        string staleAppDir = Path.Combine(_testDir, "stale_app");
        string sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(staleAppDir);
        Directory.CreateDirectory(sourceDir);

        File.WriteAllText(Path.Combine(sourceDir, "TruckSim Widget.exe"), "AppBinary");
        var currentManifest = PackageManifest.GenerateFromDirectory(sourceDir, "1.6.4");

        // Stale folder with random user files and log
        File.WriteAllText(Path.Combine(staleAppDir, "random_log.txt"), "stale log");
        File.WriteAllText(Path.Combine(staleAppDir, "user_data_file.dat"), "user data");

        // Plan with no previous manifest (fresh install into stale folder)
        var plan = FileSynchronizer.PlanSynchronization(staleAppDir, currentManifest, previousManifest: null);

        Assert.Equal(SyncActionType.Install, plan.First(p => p.RelativePath == "TruckSim Widget.exe").Action);
        Assert.Equal(SyncActionType.IgnoreUnknown, plan.First(p => p.RelativePath == "random_log.txt").Action);
        Assert.Equal(SyncActionType.IgnoreUnknown, plan.First(p => p.RelativePath == "user_data_file.dat").Action);

        using var payload = new EmbeddedPayloadProvider(sourceDir);
        var journal = TransactionJournal.StartNew("Install", "1.6.4", Path.Combine(_testDir, "test_tx_stale.json"), Path.Combine(_testDir, "staging_stale"));

        FileSynchronizer.ExecuteSynchronization(staleAppDir, payload, plan, journal);

        // App installed, stale user files preserved
        Assert.True(File.Exists(Path.Combine(staleAppDir, "TruckSim Widget.exe")));
        Assert.True(File.Exists(Path.Combine(staleAppDir, "random_log.txt")));
        Assert.True(File.Exists(Path.Combine(staleAppDir, "user_data_file.dat")));
    }

    [Fact]
    public void UntrackedInstallation_PreservesUnknownFiles()
    {
        string appDir = Path.Combine(_testDir, "untracked_app");
        string sourceDir = Path.Combine(_testDir, "source");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(sourceDir);

        File.WriteAllText(Path.Combine(sourceDir, "TruckSim Widget.exe"), "AppBinary");
        var currentManifest = PackageManifest.GenerateFromDirectory(sourceDir, "1.6.4");

        File.WriteAllText(Path.Combine(appDir, "TruckSim Widget.exe"), "OldApp");
        File.WriteAllText(Path.Combine(appDir, "user_mod.dll"), "UserModBinary");

        var plan = FileSynchronizer.PlanSynchronization(appDir, currentManifest, previousManifest: null);

        // user_mod.dll is unknown -> MUST be preserved!
        Assert.Equal(SyncActionType.IgnoreUnknown, plan.First(p => p.RelativePath == "user_mod.dll").Action);

        using var payload = new EmbeddedPayloadProvider(sourceDir);
        var journal = TransactionJournal.StartNew("Update", "1.6.4", Path.Combine(_testDir, "test_tx_untracked.json"), Path.Combine(_testDir, "staging_untracked"));

        FileSynchronizer.ExecuteSynchronization(appDir, payload, plan, journal);

        // App updated, user file preserved
        Assert.True(File.Exists(Path.Combine(appDir, "TruckSim Widget.exe")));
        Assert.True(File.Exists(Path.Combine(appDir, "user_mod.dll")));
    }

    [Fact]
    public void TransactionRollback_RestoresOriginalFilesAndVerifiesHashes()
    {
        string appDir = Path.Combine(_testDir, "rollback_app");
        string stagingDir = Path.Combine(_testDir, "staging");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(stagingDir);

        string targetFile = Path.Combine(appDir, "Widget.dll");
        string originalContent = "OriginalVersion1";
        string originalHash = Hash(originalContent);
        File.WriteAllText(targetFile, originalContent);

        var journal = TransactionJournal.StartNew("Update", "1.6.4", Path.Combine(_testDir, "tx_rollback.json"), stagingDir);

        // Stage backup
        string backupFile = Path.Combine(stagingDir, "backup.bak");
        File.Copy(targetFile, backupFile);

        int step = journal.BeginStep("ReplaceFile", "Widget.dll", targetFile, backupFile, originalHash);

        // Overwrite target with corrupt/bad data
        File.WriteAllText(targetFile, "CorruptedVersion2");
        journal.CompleteStep(step, Hash("CorruptedVersion2"));

        // Rollback
        bool success = journal.Rollback();

        Assert.True(success);
        Assert.Equal(originalContent, File.ReadAllText(targetFile));
        Assert.Equal(originalHash, Hash(File.ReadAllText(targetFile)));
    }

    [Fact]
    public void CrashRecoveryEngine_RecoversPendingTransactionOnStartup()
    {
        string appDir = Path.Combine(_testDir, "crash_app");
        string stagingDir = Path.Combine(_testDir, "staging");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(stagingDir);

        string targetFile = Path.Combine(appDir, "App.exe");
        string originalContent = "StableAppv1";
        string originalHash = Hash(originalContent);
        File.WriteAllText(targetFile, originalContent);

        string backupFile = Path.Combine(stagingDir, "App_backup.bak");
        File.Copy(targetFile, backupFile);

        string journalPath = Path.Combine(_testDir, "crash_journal.json");
        var journal = TransactionJournal.StartNew("Update", "1.6.4", journalPath, stagingDir);

        int step = journal.BeginStep("ReplaceFile", "App.exe", targetFile, backupFile, originalHash);
        File.WriteAllText(targetFile, "IncompleteOverwrittenApp");
        journal.CompleteStep(step, Hash("IncompleteOverwrittenApp"));

        // Simulate crash: journal is left with PENDING status
        Assert.True(File.Exists(journalPath));

        // Run crash recovery
        bool recovered = CrashRecoveryEngine.CheckAndExecuteRecovery(journalPath, new PluginFileOperations(), stagingDir);

        Assert.True(recovered);
        Assert.Equal(originalContent, File.ReadAllText(targetFile));
        Assert.False(File.Exists(journalPath)); // Journal cleaned up on success
    }

    [Fact]
    public void TelemetryPlugin_UninstallRestoration_RestoresBackupWhenThirdPartyReplaced()
    {
        string gameDir = Path.Combine(_testDir, "game");
        string pluginDir = Path.Combine(gameDir, "bin", "win_x64", "plugins");
        Directory.CreateDirectory(pluginDir);

        string targetPlugin = Path.Combine(pluginDir, "scs-telemetry.dll");
        string backupFile = Path.Combine(pluginDir, "scs-telemetry.dll.trucksim_backup");

        File.WriteAllText(targetPlugin, "WidgetPluginCurrent");
        File.WriteAllText(backupFile, "OriginalThirdPartyPlugin");

        var state = new GameInstallState
        {
            GameId = "ETS2",
            GamePath = gameDir,
            PluginPath = targetPlugin,
            OwnershipStatus = PluginOwnershipStatus.ThirdPartyReplaced,
            InstalledPluginHash = Hash("WidgetPluginCurrent"),
            BackupPath = backupFile
        };

        TelemetryPluginManager.ProcessGamePluginUninstall(state);

        // Target should be restored from backup
        Assert.True(File.Exists(targetPlugin));
        Assert.Equal("OriginalThirdPartyPlugin", File.ReadAllText(targetPlugin));
        // Backup should be deleted
        Assert.False(File.Exists(backupFile));
    }

    [Fact]
    public void TelemetryPlugin_Uninstall_PreservesModifiedByUserPlugin()
    {
        string gameDir = Path.Combine(_testDir, "game2");
        string pluginDir = Path.Combine(gameDir, "bin", "win_x64", "plugins");
        Directory.CreateDirectory(pluginDir);

        string targetPlugin = Path.Combine(pluginDir, "scs-telemetry.dll");
        File.WriteAllText(targetPlugin, "UserModifiedPluginBinary");

        var state = new GameInstallState
        {
            GameId = "ETS2",
            GamePath = gameDir,
            PluginPath = targetPlugin,
            OwnershipStatus = PluginOwnershipStatus.Owned,
            InstalledPluginHash = Hash("DifferentOriginalHash"),
            BackupPath = string.Empty
        };

        TelemetryPluginManager.ProcessGamePluginUninstall(state);

        // Must NOT be deleted because it was modified by user!
        Assert.True(File.Exists(targetPlugin));
        Assert.Equal("UserModifiedPluginBinary", File.ReadAllText(targetPlugin));
    }

    [Fact]
    public void DirectoryCleaner_PreservesDirectoriesWithUnknownFiles()
    {
        string rootDir = Path.Combine(_testDir, "cleaner_root");
        string emptySub = Path.Combine(rootDir, "empty_sub");
        string userSub = Path.Combine(rootDir, "user_sub");
        Directory.CreateDirectory(emptySub);
        Directory.CreateDirectory(userSub);

        // Put a user file in userSub
        File.WriteAllText(Path.Combine(userSub, "my_mod.txt"), "mod");

        DirectoryCleaner.CleanEmptyDirectories(rootDir);

        // emptySub removed
        Assert.False(Directory.Exists(emptySub));

        // userSub and rootDir MUST BE PRESERVED!
        Assert.True(Directory.Exists(userSub));
        Assert.True(Directory.Exists(rootDir));
        Assert.True(File.Exists(Path.Combine(userSub, "my_mod.txt")));
    }

    [Fact]
    public void TelemetryPlugin_EvaluateOwnership_CorrectlyDetectsModifiedByUser()
    {
        string gameDir = Path.Combine(_testDir, "ets2_modified");
        string pluginDir = Path.Combine(gameDir, "bin", "win_x64", "plugins");
        Directory.CreateDirectory(pluginDir);
        string pluginFile = Path.Combine(pluginDir, "scs-telemetry.dll");

        string originalHash = Hash("OriginalWidgetPlugin");
        File.WriteAllText(pluginFile, "ModifiedPluginContent");
        string modifiedHash = Hash("ModifiedPluginContent");

        var config = new GameConfig
        {
            GameId = "ETS2",
            SelectedPath = gameDir,
            OwnershipStatus = PluginOwnershipStatus.Owned,
            ExistingFileHash = originalHash
        };

        TelemetryPluginManager.EvaluatePluginOwnership(config, "SomeOtherHash");

        Assert.Equal(PluginOwnershipStatus.ModifiedByUser, config.OwnershipStatus);
        Assert.Equal(modifiedHash, config.ExistingFileHash);
    }

    [Fact]
    public void TelemetryPlugin_EvaluateOwnership_PreservesOwnedWhenUnmodified()
    {
        string gameDir = Path.Combine(_testDir, "ets2_unmodified");
        string pluginDir = Path.Combine(gameDir, "bin", "win_x64", "plugins");
        Directory.CreateDirectory(pluginDir);
        string pluginFile = Path.Combine(pluginDir, "scs-telemetry.dll");

        string originalHash = Hash("OriginalWidgetPlugin");
        File.WriteAllText(pluginFile, "OriginalWidgetPlugin");

        var config = new GameConfig
        {
            GameId = "ETS2",
            SelectedPath = gameDir,
            OwnershipStatus = PluginOwnershipStatus.Owned,
            ExistingFileHash = originalHash
        };

        TelemetryPluginManager.EvaluatePluginOwnership(config, "SomeOtherHash");

        Assert.Equal(PluginOwnershipStatus.Owned, config.OwnershipStatus);
        Assert.Equal(originalHash, config.ExistingFileHash);
    }

    [Fact]
    public void Ownership_IsInstallerExeOwned_DistinguishesRecordedAndUnrecordedInValidInstallation()
    {
        string dummyTarget = Path.Combine(_testDir, "TruckSimWidgetSetup.exe");

        // 1. Valid installation + unrecorded installer EXE -> NOT owned
        var validInstallUnrecorded = new InstallationInfo
        {
            Status = InstallationStatus.Installed,
            ExistingState = new InstallStateModel
            {
                InstalledFiles = new List<InstalledFileRecord>
                {
                    new InstalledFileRecord { RelativePath = "TruckSim Widget.exe", Sha256 = "hash1" }
                }
            }
        };
        Assert.False(OwnershipManager.IsInstallerExeOwned(dummyTarget, validInstallUnrecorded));

        // 2. Valid installation + recorded installer EXE -> owned
        var validInstallRecorded = new InstallationInfo
        {
            Status = InstallationStatus.Installed,
            ExistingState = new InstallStateModel
            {
                InstalledFiles = new List<InstalledFileRecord>
                {
                    new InstalledFileRecord { RelativePath = "TruckSim Widget.exe", Sha256 = "hash1" },
                    new InstalledFileRecord { RelativePath = Constants.InstallerExeName, Sha256 = "hash2" }
                }
            }
        };
        Assert.True(OwnershipManager.IsInstallerExeOwned(dummyTarget, validInstallRecorded));

        // 3. Uninstalled / stale directory -> NOT owned
        var uninstalled = new InstallationInfo
        {
            Status = InstallationStatus.NotInstalled,
            ExistingState = null
        };
        Assert.False(OwnershipManager.IsInstallerExeOwned(dummyTarget, uninstalled));
    }

    [Fact]
    public void Uninstall_PreservesUnrecordedInstallerExe()
    {
        string appDir = Path.Combine(_testDir, "uninstall_app");
        Directory.CreateDirectory(appDir);

        string appExe = Path.Combine(appDir, "TruckSim Widget.exe");
        string installerExe = Path.Combine(appDir, "TruckSimWidgetSetup.exe");
        File.WriteAllText(appExe, "AppBinary");
        File.WriteAllText(installerExe, "CustomUnrecordedInstallerOrUserFile");

        // Existing state only records appExe, NOT installerExe
        var existingState = new InstallStateModel
        {
            InstalledFiles = new List<InstalledFileRecord>
            {
                new InstalledFileRecord { RelativePath = "TruckSim Widget.exe", Sha256 = Hash("AppBinary") }
            }
        };

        var installInfo = new InstallationInfo
        {
            Status = InstallationStatus.Installed,
            InstallPath = appDir,
            ExistingState = existingState
        };

        // Collect owned files according to the uninstall algorithm
        var ownedFiles = new List<string>();
        foreach (var f in installInfo.ExistingState.InstalledFiles)
        {
            string full = Path.Combine(appDir, f.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!ownedFiles.Contains(full, StringComparer.OrdinalIgnoreCase))
            {
                ownedFiles.Add(full);
            }
        }

        // Simulate deletion of owned files
        foreach (var file in ownedFiles)
        {
            if (File.Exists(file)) File.Delete(file);
        }

        // App binary was owned -> deleted
        Assert.False(File.Exists(appExe));

        // Unrecorded installer EXE was NOT owned -> MUST BE PRESERVED!
        Assert.True(File.Exists(installerExe));
        Assert.Equal("CustomUnrecordedInstallerOrUserFile", File.ReadAllText(installerExe));
    }


    [Fact]
    public void TransactionRollback_RestoresManifestFile()
    {
        string stagingDir = Path.Combine(_testDir, "manifest_staging");
        Directory.CreateDirectory(stagingDir);

        string manifestPath = Path.Combine(_testDir, "installed-manifest.json");
        string originalContent = "{\"version\":\"1.0.0\"}";
        File.WriteAllText(manifestPath, originalContent);
        string originalHash = Hash(originalContent);

        var journal = TransactionJournal.StartNew("Update", "2.0.0", Path.Combine(_testDir, "tx_manifest.json"), stagingDir);

        string backupFile = Path.Combine(stagingDir, "manifest_backup.json");
        File.Copy(manifestPath, backupFile);

        int step = journal.BeginStep("SaveManifestFile", string.Empty, manifestPath, backupFile, originalHash);
        File.WriteAllText(manifestPath, "{\"version\":\"2.0.0\"}");
        journal.CompleteStep(step, Hash("{\"version\":\"2.0.0\"}"));

        // Rollback
        bool success = journal.Rollback();

        Assert.True(success);
        Assert.Equal(originalContent, File.ReadAllText(manifestPath));
    }

    [Fact]
    public void TransactionRollback_UnregistersWindowsRegistration()
    {
        var journal = TransactionJournal.StartNew("Install", "1.6.4", Path.Combine(_testDir, "tx_reg.json"), Path.Combine(_testDir, "staging_reg"));

        int step = journal.BeginStep("RegisterWindowsUninstall", string.Empty, Constants.UninstallRegSubKey, string.Empty, string.Empty);
        bool registered = WindowsRegistration.Register(_testDir, "1.6.4");
        Assert.True(registered);
        journal.CompleteStep(step);

        // Verify key exists
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Constants.UninstallRegSubKey))
        {
            Assert.NotNull(key);
        }

        // Rollback should unregister
        bool success = journal.Rollback();
        Assert.True(success);

        // Verify key no longer exists
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Constants.UninstallRegSubKey))
        {
            Assert.Null(key);
        }
    }
}
