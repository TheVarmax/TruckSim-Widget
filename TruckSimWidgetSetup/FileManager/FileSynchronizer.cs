using System.IO;
using TruckSimWidgetSetup.Diagnostics;
using TruckSimWidgetSetup.InstallationState;
using TruckSimWidgetSetup.TransactionEngine;

namespace TruckSimWidgetSetup.FileManager;

public enum SyncActionType
{
    Install,
    Replace,
    Verified,
    DeleteObsolete,
    IgnoreUnknown
}

public class SyncFilePlan
{
    public SyncActionType Action { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string TargetFullPath { get; set; } = string.Empty;
    public string ExpectedSha256 { get; set; } = string.Empty;
    public string CurrentSha256 { get; set; } = string.Empty;
}

public static class FileSynchronizer
{
    public static List<SyncFilePlan> PlanSynchronization(
        string targetAppDir,
        PackageManifest currentManifest,
        PackageManifest? previousManifest,
        bool isLegacyTakeover)
    {
        var plan = new List<SyncFilePlan>();
        var seenDiskFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan current manifest files against disk
        foreach (var entry in currentManifest.Files)
        {
            string normRel = PackageManifest.NormalizeRelativePath(entry.RelativePath);
            string fullPath = Path.Combine(targetAppDir, normRel.Replace('/', Path.DirectorySeparatorChar));
            seenDiskFiles.Add(normRel);

            if (!File.Exists(fullPath))
            {
                plan.Add(new SyncFilePlan
                {
                    Action = SyncActionType.Install,
                    RelativePath = normRel,
                    TargetFullPath = fullPath,
                    ExpectedSha256 = entry.Sha256
                });
            }
            else
            {
                string diskHash = PackageManifest.ComputeFileSha256(fullPath);
                if (string.Equals(diskHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    plan.Add(new SyncFilePlan
                    {
                        Action = SyncActionType.Verified,
                        RelativePath = normRel,
                        TargetFullPath = fullPath,
                        ExpectedSha256 = entry.Sha256,
                        CurrentSha256 = diskHash
                    });
                }
                else
                {
                    plan.Add(new SyncFilePlan
                    {
                        Action = SyncActionType.Replace,
                        RelativePath = normRel,
                        TargetFullPath = fullPath,
                        ExpectedSha256 = entry.Sha256,
                        CurrentSha256 = diskHash
                    });
                }
            }
        }

        // 2. Scan disk files not present in current manifest
        if (Directory.Exists(targetAppDir))
        {
            var diskFiles = Directory.GetFiles(targetAppDir, "*", SearchOption.AllDirectories);
            foreach (var fullPath in diskFiles)
            {
                string rel = Path.GetRelativePath(targetAppDir, fullPath);
                string normRel = PackageManifest.NormalizeRelativePath(rel);

                if (seenDiskFiles.Contains(normRel)) continue;

                bool isOwned = OwnershipManager.IsFileOwned(
                    normRel,
                    fullPath,
                    currentManifest,
                    previousManifest,
                    isLegacyTakeover);

                if (isOwned)
                {
                    string diskHash = PackageManifest.ComputeFileSha256(fullPath);
                    plan.Add(new SyncFilePlan
                    {
                        Action = SyncActionType.DeleteObsolete,
                        RelativePath = normRel,
                        TargetFullPath = fullPath,
                        CurrentSha256 = diskHash
                    });
                }
                else
                {
                    plan.Add(new SyncFilePlan
                    {
                        Action = SyncActionType.IgnoreUnknown,
                        RelativePath = normRel,
                        TargetFullPath = fullPath
                    });
                    InstallerLogger.LogIgnoredUserFile(fullPath);
                }
            }
        }

        return plan;
    }

    public static List<InstalledFileRecord> ExecuteSynchronization(
        string targetAppDir,
        EmbeddedPayloadProvider payloadProvider,
        List<SyncFilePlan> plan,
        TransactionJournal journal,
        IProgress<double>? progress = null)
    {
        var installedRecords = new List<InstalledFileRecord>();
        string stagingDir = journal.StagingDir;
        if (!Directory.Exists(stagingDir)) Directory.CreateDirectory(stagingDir);

        int totalOperations = plan.Count(p => p.Action != SyncActionType.IgnoreUnknown && p.Action != SyncActionType.Verified);
        int currentOp = 0;

        foreach (var item in plan)
        {
            switch (item.Action)
            {
                case SyncActionType.Verified:
                    installedRecords.Add(new InstalledFileRecord
                    {
                        RelativePath = item.RelativePath,
                        Sha256 = item.ExpectedSha256
                    });
                    break;

                case SyncActionType.Install:
                    {
                        string? dir = Path.GetDirectoryName(item.TargetFullPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            int dirStep = journal.BeginStep("CreateDir", string.Empty, dir, string.Empty, string.Empty);
                            Directory.CreateDirectory(dir);
                            journal.CompleteStep(dirStep);
                        }

                        int stepIdx = journal.BeginStep("CopyFile", item.RelativePath, item.TargetFullPath, string.Empty, string.Empty);
                        payloadProvider.ExtractFile(item.RelativePath, item.TargetFullPath);

                        string actualHash = PackageManifest.ComputeFileSha256(item.TargetFullPath);
                        if (!string.Equals(actualHash, item.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException($"SHA-256 verification failed for installed file: {item.RelativePath}");
                        }

                        journal.CompleteStep(stepIdx, actualHash);
                        installedRecords.Add(new InstalledFileRecord
                        {
                            RelativePath = item.RelativePath,
                            Sha256 = actualHash
                        });

                        currentOp++;
                        progress?.Report((double)currentOp / Math.Max(1, totalOperations));
                        break;
                    }

                case SyncActionType.Replace:
                    {
                        string stagingBackup = Path.Combine(stagingDir, $"replace_{Guid.NewGuid():N}.bak");
                        File.Copy(item.TargetFullPath, stagingBackup, overwrite: true);

                        int stepIdx = journal.BeginStep("ReplaceFile", item.RelativePath, item.TargetFullPath, stagingBackup, item.CurrentSha256);
                        payloadProvider.ExtractFile(item.RelativePath, item.TargetFullPath);

                        string actualHash = PackageManifest.ComputeFileSha256(item.TargetFullPath);
                        if (!string.Equals(actualHash, item.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException($"SHA-256 verification failed for replaced file: {item.RelativePath}");
                        }

                        journal.CompleteStep(stepIdx, actualHash);
                        installedRecords.Add(new InstalledFileRecord
                        {
                            RelativePath = item.RelativePath,
                            Sha256 = actualHash
                        });

                        currentOp++;
                        progress?.Report((double)currentOp / Math.Max(1, totalOperations));
                        break;
                    }

                case SyncActionType.DeleteObsolete:
                    {
                        string stagingBackup = Path.Combine(stagingDir, $"delete_{Guid.NewGuid():N}.bak");
                        File.Copy(item.TargetFullPath, stagingBackup, overwrite: true);

                        int stepIdx = journal.BeginStep("DeleteFile", item.RelativePath, item.TargetFullPath, stagingBackup, item.CurrentSha256);
                        File.Delete(item.TargetFullPath);
                        journal.CompleteStep(stepIdx);

                        InstallerLogger.LogInfo($"Deleted obsolete installer-owned file: {item.TargetFullPath}");
                        currentOp++;
                        progress?.Report((double)currentOp / Math.Max(1, totalOperations));
                        break;
                    }

                case SyncActionType.IgnoreUnknown:
                    // Intentionally untouched
                    break;
            }
        }

        // Clean any empty directories after deletions
        DirectoryCleaner.CleanEmptyDirectories(targetAppDir);

        return installedRecords;
    }
}
