using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;
using TruckSimWidgetSetup.FileManager;
using TruckSimWidgetSetup.GameDiscovery;
using TruckSimWidgetSetup.InstallationState;
using TruckSimWidgetSetup.PluginManager;
using TruckSimWidgetSetup.TransactionEngine;
using Xunit;

namespace TruckSimWidget.Tests;

[Collection("Installer integrity")]
public sealed class PluginStagingIntegrityTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("tsw_i4_tests_").FullName;
    private readonly List<TransactionJournal> _journals = new();
    private readonly List<string> _legacyRoots = new();

    public void Dispose()
    {
        foreach (var journal in _journals) journal.CleanupStagingDir();
        foreach (string root in _legacyRoots) if (Directory.Exists(root)) Directory.Delete(root, true);
        ElevatedHelperRunner.Reset();
        Directory.Delete(_root, recursive: true);
    }

    private TransactionJournal NewJournal()
    {
        string state = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var journal = TransactionJournal.StartNew("Install", "1.6.5",
            Path.Combine(state, "transaction_journal.json"), Path.Combine(state, "staging"));
        _journals.Add(journal);
        return journal;
    }

    private GameConfig Game(string? existing = null)
    {
        var game = GameConfig.CreateEts2();
        game.SelectedPath = Path.Combine(_root, "game");
        game.UserSelected = true;
        game.ConflictAction = PluginConflictAction.BackupReplace;
        Directory.CreateDirectory(Path.GetDirectoryName(game.TargetPluginFile)!);
        if (existing != null) File.WriteAllText(game.TargetPluginFile, existing);
        return game;
    }

    private EmbeddedPayloadProvider Payload()
    {
        string source = Path.Combine(_root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "TruckSim Widget.exe"), "app");
        return new EmbeddedPayloadProvider(sourceDirectory: source);
    }

    private static string Hash(string file) => PackageManifest.ComputeFileSha256(file);

    private static bool HelperAllows(string path, bool sourceOnly)
    {
        Type helper = Assembly.Load("ElevatedHelper").GetType("TruckSimWidget.ElevatedHelper.Program", throwOnError: true)!;
        var method = helper.GetMethod("IsAllowedPath", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(string), typeof(bool), typeof(bool), typeof(List<string>) }, null)!;
        return (bool)method.Invoke(null, new object[] { path, false, sourceOnly, new List<string>() })!;
    }

    [Fact]
    public void StagingIsUniqueTempSourceAndSurvivesJournalSerialization()
    {
        var first = NewJournal();
        var second = NewJournal();
        string staging = first.GetPluginStagingDir();
        Assert.NotEqual(staging, second.GetPluginStagingDir());
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(staging));
        Assert.StartsWith("TruckSimWidget_Staging_", Path.GetFileName(staging));
        Assert.NotEqual(first.StagingDir, staging);
        Assert.StartsWith(_root, first.JournalFilePath);
        foreach (string name in new[] { "ETS2_new_plugin.dll", "ATS_new_plugin.dll", "ETS2_rollback_20261006_120000.dll", "ATS_rollback_20261006_120000.dll" })
        {
            string file = Path.Combine(staging, name);
            File.WriteAllText(file, "plugin");
            Assert.True(HelperAllows(file, sourceOnly: true));
            Assert.False(HelperAllows(file, sourceOnly: false));
        }
        Assert.False(HelperAllows(Path.Combine(staging, "arbitrary.exe"), true));
        var restored = JsonSerializer.Deserialize<TransactionJournal>(File.ReadAllText(first.JournalFilePath), TransactionJournal.JsonOptions)!;
        Assert.Equal(staging, restored.PluginStagingDir);
        first.Commit();
        Assert.False(Directory.Exists(staging));
    }

    [Fact]
    public void TamperedStagedFileNeverReachesCopyOrElevation()
    {
        using var staging = PluginStagingDirectory.Create();
        string file = Path.Combine(staging.Path, "ETS2_new_plugin.dll");
        File.WriteAllText(file, "trusted plugin");
        string expected = Hash(file);
        File.WriteAllText(file, "tampered plugin");
        int copies = 0, elevations = 0;
        var files = new PluginFileOperations((_, _) => copies++, (_, _) => { elevations++; return true; });
        Assert.Equal(PluginCopyResult.IntegrityFailed, files.CopyVerified(file, Path.Combine(_root, "target.dll"), expected));
        Assert.Equal(0, copies);
        Assert.Equal(0, elevations);
        Assert.False(File.Exists(Path.Combine(_root, "target.dll")));
    }

    [Fact]
    public void ValidElevatedSourceIsPinnedThroughHelperReadAndDestinationIsVerified()
    {
        using var staging = PluginStagingDirectory.Create();
        string source = Path.Combine(staging.Path, "ETS2_new_plugin.dll");
        string target = Game().TargetPluginFile;
        File.WriteAllText(source, "trusted plugin");
        string expected = Hash(source);
        int elevations = 0;
        var files = new PluginFileOperations((_, _) => throw new UnauthorizedAccessException(), (actualSource, actualTarget) =>
        {
            elevations++;
            Assert.Equal(source, actualSource);
            Assert.Equal(target, actualTarget);
            Assert.True(HelperAllows(actualSource, true));
            Assert.Throws<IOException>(() => File.WriteAllText(actualSource, "replacement"));
            Assert.Throws<IOException>(() => File.Delete(actualSource));
            Assert.Throws<IOException>(() => File.Move(actualSource, actualSource + ".moved"));
            Assert.Throws<IOException>(() => Directory.Move(staging.Path, staging.Path + "_moved"));
            // This is the helper's actual file-sharing contract.
            using var input = new FileStream(actualSource, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var output = new FileStream(actualTarget, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
            output.Flush(true);
            return true;
        });
        Assert.Equal(PluginCopyResult.Success, files.CopyVerified(source, target, expected));
        Assert.Equal(1, elevations);
        Assert.Equal(expected, Hash(target));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DestinationMismatchFailsWithoutOwnershipAndRollbackRestoresOriginal(bool update)
    {
        using var payload = Payload();
        var game = Game("third-party bytes");
        string originalHash = Hash(game.TargetPluginFile);
        var journal = NewJournal();
        var files = new PluginFileOperations((source, target) =>
        {
            if (source.EndsWith("_new_plugin.dll", StringComparison.Ordinal)) throw new UnauthorizedAccessException();
            File.Copy(source, target, true);
        }, (_, target) => { File.WriteAllText(target, "wrong destination"); return true; });
        Assert.False(TelemetryPluginManager.InstallPluginForGame(game, payload, journal, update, files));
        Assert.Equal(PluginOwnershipStatus.None, game.OwnershipStatus);
        Assert.Equal("STEP_PENDING", journal.Steps.Single(step => step.Operation == "CopyPlugin").Status);
        // InstallerService uses this failure-to-rollback flow and never saves state.
        Assert.True(journal.Rollback());
        Assert.Equal(originalHash, Hash(game.TargetPluginFile));
        Assert.Equal("third-party bytes", File.ReadAllText(game.TargetPluginFile));
        Assert.False(Directory.Exists(journal.PluginStagingDir));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UacCancellationFailsAndRollbackCleansTemporaryFiles(bool update)
    {
        using var payload = Payload();
        var game = Game("original");
        var journal = NewJournal();
        int elevations = 0;
        var files = new PluginFileOperations((source, target) =>
        {
            if (source.EndsWith("_new_plugin.dll", StringComparison.Ordinal)) throw new UnauthorizedAccessException();
            File.Copy(source, target, true);
        }, (_, _) => { elevations++; return false; });
        Assert.False(TelemetryPluginManager.InstallPluginForGame(game, payload, journal, update, files));
        Assert.Equal(1, elevations);
        Assert.Equal(PluginOwnershipStatus.None, game.OwnershipStatus);
        Assert.True(journal.Rollback());
        Assert.Equal("original", File.ReadAllText(game.TargetPluginFile));
        Assert.False(Directory.Exists(journal.PluginStagingDir));
    }

    [Fact]
    public void WritableLibraryInstallsWithoutElevationAndUninstallRestoresThirdPartyBytes()
    {
        using var payload = Payload();
        var game = Game("original third-party bytes");
        var journal = NewJournal();
        using var stream = payload.OpenTelemetryPluginStream()!;
        string expected = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        var files = new PluginFileOperations(elevatedCopy: (_, _) => throw new Exception("Writable library must not elevate."));
        Assert.True(TelemetryPluginManager.InstallPluginForGame(game, payload, journal, false, files));
        Assert.Equal(expected, Hash(game.TargetPluginFile));
        Assert.Equal(expected, game.ExistingFileHash);
        Assert.Equal(PluginOwnershipStatus.ThirdPartyReplaced, game.OwnershipStatus);
        Assert.Equal("original third-party bytes", File.ReadAllText(game.BackupPath));
        journal.Commit();
        Assert.False(Directory.Exists(journal.PluginStagingDir));
        var state = new GameInstallState { GameId = game.GameId, PluginPath = game.TargetPluginFile,
            OwnershipStatus = game.OwnershipStatus, InstalledPluginHash = game.ExistingFileHash, BackupPath = game.BackupPath };
        TelemetryPluginManager.ProcessGamePluginUninstall(state, files);
        Assert.Equal("original third-party bytes", File.ReadAllText(game.TargetPluginFile));
        Assert.False(File.Exists(game.BackupPath));
    }

    [Fact]
    public void ProtectedUninstallRestagesOriginalBackupAndVerifiesDestination()
    {
        var game = Game("widget plugin");
        string backup = game.TargetPluginFile + Constants.PluginBackupExt;
        File.WriteAllText(backup, "third-party bytes");
        string? staged = null;
        var files = new PluginFileOperations((_, _) => throw new UnauthorizedAccessException(), (source, target) =>
        {
            staged = source;
            Assert.True(PluginStagingDirectory.IsOwnedPath(Path.GetDirectoryName(source)!));
            Assert.True(HelperAllows(source, true));
            File.Copy(source, target, true);
            return true;
        });
        var state = new GameInstallState { GameId = game.GameId, PluginPath = game.TargetPluginFile,
            OwnershipStatus = PluginOwnershipStatus.ThirdPartyReplaced, InstalledPluginHash = Hash(game.TargetPluginFile), BackupPath = backup };
        TelemetryPluginManager.ProcessGamePluginUninstall(state, files);
        Assert.Equal("third-party bytes", File.ReadAllText(game.TargetPluginFile));
        Assert.False(File.Exists(backup));
        Assert.NotNull(staged);
        Assert.False(Directory.Exists(Path.GetDirectoryName(staged)));
    }

    [Fact]
    public void UninstallDestinationMismatchPreservesBackupAndReportsFailure()
    {
        var game = Game("widget plugin");
        string backup = game.TargetPluginFile + Constants.PluginBackupExt;
        File.WriteAllText(backup, "third-party bytes");
        var files = new PluginFileOperations((_, _) => throw new UnauthorizedAccessException(), (_, target) =>
        { File.WriteAllText(target, "wrong contents"); return true; });
        var state = new GameInstallState { PluginPath = game.TargetPluginFile, OwnershipStatus = PluginOwnershipStatus.ThirdPartyReplaced,
            InstalledPluginHash = Hash(game.TargetPluginFile), BackupPath = backup };
        Assert.Throws<IOException>(() => TelemetryPluginManager.ProcessGamePluginUninstall(state, files));
        Assert.Equal("third-party bytes", File.ReadAllText(backup));
    }

    [Fact]
    public void OldJournalRecoveryRestagesLegacyBackupAndRestoresByteForByte()
    {
        var game = Game("third-party bytes");
        string originalHash = Hash(game.TargetPluginFile);
        var journal = NewJournal();
        // Isolated LocalAppData root, with the previous installer/staging layout;
        // never touch this computer's actual Widget transaction or staging files.
        string legacyRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TruckSimWidget_I4Tests_" + Guid.NewGuid().ToString("N"));
        _legacyRoots.Add(legacyRoot);
        journal.StagingDir = Path.Combine(legacyRoot, "installer", "staging");
        // Pre-I4 schema: no pluginStagingDir; source/backup point into installer/staging.
        string legacy = Path.Combine(journal.StagingDir, "ETS2_rollback_20261006_120000.dll");
        Directory.CreateDirectory(journal.StagingDir);
        File.Copy(game.TargetPluginFile, legacy);
        Assert.False(HelperAllows(legacy, true));
        journal.BeginStep("CopyPlugin", Path.Combine(journal.StagingDir, "ETS2_new_plugin.dll"), game.TargetPluginFile, legacy, originalHash);
        File.WriteAllText(game.TargetPluginFile, "interrupted installation");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(journal.JournalFilePath))!.AsObject();
        json.Remove("pluginStagingDir");
        File.WriteAllText(journal.JournalFilePath, json.ToJsonString());
        string? staged = null;
        var files = new PluginFileOperations((_, _) => throw new UnauthorizedAccessException(), (source, target) =>
        {
            staged = source;
            Assert.NotEqual(legacy, source);
            Assert.True(HelperAllows(source, true));
            File.Copy(source, target, true);
            return true;
        });
        Assert.True(CrashRecoveryEngine.CheckAndExecuteRecovery(journal.JournalFilePath, files, journal.StagingDir));
        Assert.Equal(originalHash, Hash(game.TargetPluginFile));
        Assert.False(File.Exists(journal.JournalFilePath));
        Assert.NotNull(staged);
        Assert.False(Directory.Exists(Path.GetDirectoryName(staged)));
    }

    [Fact]
    public void CancelledElevationDoesNotRequireAnotherUacToRollbackUnchangedOriginal()
    {
        using var payload = Payload();
        var game = Game("original bytes");
        game.ConflictAction = PluginConflictAction.Overwrite;
        var journal = NewJournal();
        int elevations = 0;
        var files = new PluginFileOperations((_, _) => throw new UnauthorizedAccessException(), (_, _) => { elevations++; return false; });
        Assert.False(TelemetryPluginManager.InstallPluginForGame(game, payload, journal, false, files));
        Assert.True(journal.Rollback(files));
        Assert.Equal(1, elevations);
        Assert.Equal("original bytes", File.ReadAllText(game.TargetPluginFile));
        Assert.False(Directory.Exists(journal.PluginStagingDir));
    }

    [Fact]
    public void DestinationLockedAfterCopyFailsAndDoesNotCommitAsSuccessfulSkip()
    {
        using var payload = Payload();
        var game = Game("original bytes");
        game.ConflictAction = PluginConflictAction.Overwrite;
        var journal = NewJournal();
        FileStream? destinationLock = null;
        try
        {
            var files = new PluginFileOperations((_, _) => throw new UnauthorizedAccessException(), (_, target) =>
            {
                File.WriteAllText(target, "unverified bytes");
                destinationLock = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None);
                return true;
            });
            Assert.False(TelemetryPluginManager.InstallPluginForGame(game, payload, journal, true, files));
            Assert.Equal(PluginOwnershipStatus.None, game.OwnershipStatus);
            Assert.Equal("STEP_PENDING", journal.Steps.Single(step => step.Operation == "CopyPlugin").Status);
        }
        finally { destinationLock?.Dispose(); }
        Assert.True(journal.Rollback());
        Assert.Equal("original bytes", File.ReadAllText(game.TargetPluginFile));
    }

    [Fact]
    public void LockedOriginalGamePluginSkipsBeforeMutationWithoutUac()
    {
        using var payload = Payload();
        var game = Game("original bytes");
        game.ConflictAction = PluginConflictAction.Overwrite;
        var journal = NewJournal();
        var files = new PluginFileOperations(elevatedCopy: (_, _) => throw new Exception("No elevation for locked game."));
        using (var gameLock = new FileStream(game.TargetPluginFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.True(TelemetryPluginManager.InstallPluginForGame(game, payload, journal, true, files));
        Assert.DoesNotContain(journal.Steps, step => step.Operation == "CopyPlugin");
        Assert.Equal("original bytes", File.ReadAllText(game.TargetPluginFile));
        journal.Commit();
        Assert.False(Directory.Exists(journal.PluginStagingDir));
    }

    [Fact]
    public void FailedProtectedRecoveryPreservesHelperForSuccessfulRetry()
    {
        var journal = NewJournal();
        string helper = Path.Combine(_root, "ElevatedHelper.exe");
        File.WriteAllText(helper, "trusted helper payload");
        int helperStep = journal.BeginStep("CopyFile", "ElevatedHelper.exe", helper, "", "");
        journal.CompleteStep(helperStep, Hash(helper));
        var game = Game("third-party original");
        string backup = Path.Combine(journal.GetPluginStagingDir(), "ETS2_rollback_20261006_120000.dll");
        File.Copy(game.TargetPluginFile, backup);
        journal.BeginStep("CopyPlugin", "unused", game.TargetPluginFile, backup, Hash(backup));
        File.WriteAllText(game.TargetPluginFile, "interrupted widget copy");
        var cancelled = new PluginFileOperations((_, _) => throw new UnauthorizedAccessException(), (_, _) => false);
        Assert.False(journal.Rollback(cancelled));
        Assert.True(File.Exists(helper));
        Assert.True(File.Exists(journal.JournalFilePath));
        var retry = new PluginFileOperations((_, _) => throw new UnauthorizedAccessException(), (source, target) =>
        {
            Assert.True(File.Exists(helper));
            Assert.Equal(journal.Steps[helperStep].NewHash, Hash(helper));
            File.Copy(source, target, true);
            return true;
        });
        Assert.True(CrashRecoveryEngine.CheckAndExecuteRecovery(journal.JournalFilePath, retry, journal.StagingDir));
        Assert.Equal("third-party original", File.ReadAllText(game.TargetPluginFile));
        Assert.False(File.Exists(journal.JournalFilePath));
        Assert.False(File.Exists(helper)); // app rollback runs only after plugin restore succeeds
        Assert.False(Directory.Exists(journal.PluginStagingDir));
    }

    [Fact]
    public void RecoveryHelperComesFromEmbeddedPayloadAndIsPinnedThroughUse()
    {
        using var payload = new EmbeddedPayloadProvider();
        Assert.True(payload.Manifest.TryGetEntry("ElevatedHelper.exe", out var entry));
        ElevatedHelperRunner.Reset();
        string? usedHelper = null;
        Assert.True(ElevatedHelperRunner.WithTrustedRecoveryHelper((helper, digest) =>
        {
            usedHelper = helper;
            Assert.Equal(entry!.Sha256, digest);
            Assert.Equal(digest, Hash(helper), ignoreCase: true);
            Assert.True(PluginStagingDirectory.IsOwnedPath(Path.GetDirectoryName(helper)!));
            Assert.Throws<IOException>(() => File.WriteAllText(helper, "tampered executable"));
            Assert.Throws<IOException>(() => File.Delete(helper));
            Assert.Throws<IOException>(() => Directory.Move(Path.GetDirectoryName(helper)!, Path.GetDirectoryName(helper)! + "_moved"));
            return true;
        }));
        Assert.NotNull(usedHelper);
        Assert.False(Directory.Exists(Path.GetDirectoryName(usedHelper)));
        Assert.Null(typeof(ElevatedHelperRunner).GetField("_helperPath", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null));
    }

    [Fact]
    public void RecoveryHelperFailureCleansItsTemporaryDirectory()
    {
        string? helperDirectory = null;
        Assert.False(ElevatedHelperRunner.WithTrustedRecoveryHelper((helper, _) =>
        {
            helperDirectory = Path.GetDirectoryName(helper);
            return false; // cancellation/failure after preparing the trusted helper
        }));
        Assert.NotNull(helperDirectory);
        Assert.False(Directory.Exists(helperDirectory));
    }

    [Fact]
    public void CorruptRollbackBackupDoesNotDestroyTargetAndRecoveryRetainsJournal()
    {
        var game = Game("original");
        var journal = NewJournal();
        string backup = Path.Combine(journal.GetPluginStagingDir(), "ETS2_rollback_20261006_120000.dll");
        File.Copy(game.TargetPluginFile, backup);
        string expected = Hash(game.TargetPluginFile);
        journal.BeginStep("CopyPlugin", "unused", game.TargetPluginFile, backup, expected);
        File.WriteAllText(game.TargetPluginFile, "current target");
        File.WriteAllText(backup, "tampered backup");
        Assert.False(CrashRecoveryEngine.CheckAndExecuteRecovery(journal.JournalFilePath, new PluginFileOperations(), journal.StagingDir));
        Assert.Equal("current target", File.ReadAllText(game.TargetPluginFile));
        Assert.True(File.Exists(journal.JournalFilePath));
        Assert.True(File.Exists(backup));
    }

    [Fact]
    public void RecoveryOfNewJournalCleansPersistedTempStaging()
    {
        var game = Game();
        var journal = NewJournal();
        string staged = Path.Combine(journal.GetPluginStagingDir(), "ETS2_new_plugin.dll");
        File.WriteAllText(staged, "plugin");
        journal.BeginStep("CopyPlugin", staged, game.TargetPluginFile, "", "");
        File.WriteAllText(game.TargetPluginFile, "partially installed plugin");
        Assert.True(CrashRecoveryEngine.CheckAndExecuteRecovery(journal.JournalFilePath, new PluginFileOperations(), journal.StagingDir));
        Assert.False(File.Exists(game.TargetPluginFile));
        Assert.False(Directory.Exists(journal.PluginStagingDir));
    }

    [Fact]
    public void CleanupRejectsForeignDirectoriesAndNeverTraversesNestedDirectories()
    {
        string foreign = Path.Combine(_root, "foreign");
        Directory.CreateDirectory(foreign);
        File.WriteAllText(Path.Combine(foreign, "keep.txt"), "user bytes");
        PluginStagingDirectory.Cleanup(foreign);
        Assert.True(File.Exists(Path.Combine(foreign, "keep.txt")));
        using var staging = PluginStagingDirectory.Create();
        // Directory entries are never recursively traversed by staging cleanup.
        Directory.CreateDirectory(Path.Combine(staging.Path, "nested"));
        PluginStagingDirectory.Cleanup(staging.Path);
        Assert.True(Directory.Exists(staging.Path));
        Directory.Delete(Path.Combine(staging.Path, "nested"));
    }
}

[CollectionDefinition("Installer integrity", DisableParallelization = true)]
public sealed class InstallerIntegrityCollection : ICollectionFixture<InstallerIntegrityEnvironment> { }

public sealed class InstallerIntegrityEnvironment : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("tsw_i4_test_logs_").FullName;
    private readonly string _previous = InstallerLogger.LogFilePath;
    public InstallerIntegrityEnvironment() => InstallerLogger.SetCustomLogPath(Path.Combine(_root, "installer.log"));
    public void Dispose()
    {
        InstallerLogger.SetCustomLogPath(_previous);
        Directory.Delete(_root, true);
    }
}
