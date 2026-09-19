using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;
using TruckSimWidgetSetup.FileManager;

namespace TruckSimWidgetSetup.TransactionEngine;

public class TransactionJournal
{
    private readonly object _lock = new();

    [JsonPropertyName("transactionId")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "PENDING";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

    [JsonPropertyName("steps")]
    public List<TransactionStep> Steps { get; set; } = new();

    [JsonIgnore]
    public string JournalFilePath { get; set; } = Constants.GetTransactionJournalFilePath();

    [JsonIgnore]
    public string StagingDir { get; set; } = Constants.GetTransactionStagingDir();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static TransactionJournal StartNew(string operation, string version)
    {
        var journal = new TransactionJournal
        {
            TransactionId = $"tx_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}",
            Operation = operation,
            Version = version,
            Status = "PENDING",
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };

        journal.FlushToDisk();
        InstallerLogger.LogInfo($"Initialized transaction: {journal.TransactionId} ({operation} v{version})");
        return journal;
    }

    public int BeginStep(string op, string source, string target, string backup, string originalHash)
    {
        lock (_lock)
        {
            int idx = Steps.Count;
            var step = new TransactionStep
            {
                StepIndex = idx,
                Operation = op,
                SourcePath = source,
                TargetPath = target,
                BackupPath = backup,
                OriginalHash = originalHash,
                Status = "STEP_PENDING"
            };

            Steps.Add(step);
            FlushToDisk();
            return idx;
        }
    }

    public void CompleteStep(int stepIndex, string? newHash = null)
    {
        lock (_lock)
        {
            if (stepIndex >= 0 && stepIndex < Steps.Count)
            {
                Steps[stepIndex].Status = "STEP_COMPLETED";
                if (!string.IsNullOrEmpty(newHash))
                {
                    Steps[stepIndex].NewHash = newHash;
                }

                FlushToDisk();
            }
        }
    }

    public void FlushToDisk()
    {
        lock (_lock)
        {
            try
            {
                string? dir = Path.GetDirectoryName(JournalFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(this, JsonOptions);
                string tmpPath = JournalFilePath + ".tmp";

                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, JournalFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                InstallerLogger.LogErr($"Failed to atomically write transaction journal: {ex.Message}");
            }
        }
    }

    public bool Rollback()
    {
        lock (_lock)
        {
            InstallerLogger.LogWarn($"Executing transaction rollback for: {TransactionId}");
            bool allSucceeded = true;

            for (int i = Steps.Count - 1; i >= 0; i--)
            {
                var step = Steps[i];
                if (step.Status != "STEP_COMPLETED") continue;

                try
                {
                    if (!ExecuteStepRollback(step))
                    {
                        allSucceeded = false;
                        InstallerLogger.LogErr($"Rollback failed for step {step.StepIndex} ({step.Operation}): {step.TargetPath}");
                    }
                }
                catch (Exception ex)
                {
                    allSucceeded = false;
                    InstallerLogger.LogErr($"Exception rolling back step {step.StepIndex}: {ex.Message}");
                }
            }

            if (allSucceeded)
            {
                Status = "ROLLED_BACK";
                FlushToDisk();
                InstallerLogger.LogInfo($"Rollback completed successfully for {TransactionId}.");

                // Clean staging directory
                CleanupStagingDir();
            }
            else
            {
                InstallerLogger.LogErr($"Rollback encountered errors for {TransactionId}. Journal left in PENDING for diagnostics.");
            }

            return allSucceeded;
        }
    }

    public void Commit()
    {
        lock (_lock)
        {
            Status = "COMMITTED";
            FlushToDisk();
            InstallerLogger.LogInfo($"Transaction {TransactionId} committed successfully.");

            // Clean staging directory
            CleanupStagingDir();

            // Remove committed journal file
            try
            {
                if (File.Exists(JournalFilePath))
                {
                    File.Delete(JournalFilePath);
                }
            }
            catch { }
        }
    }

    private void CleanupStagingDir()
    {
        try
        {
            if (Directory.Exists(StagingDir))
            {
                Directory.Delete(StagingDir, recursive: true);
            }
        }
        catch { }
    }

    internal static bool ExecuteStepRollback(TransactionStep step)
    {
        InstallerLogger.LogInfo($"Rolling back step {step.StepIndex} ({step.Operation}): {step.TargetPath}");

        switch (step.Operation)
        {
            case "CopyFile":
            case "CopyPlugin":
                // If target was created, delete it
                if (File.Exists(step.TargetPath))
                {
                    File.Delete(step.TargetPath);
                }

                // If a backup existed before this copy, restore it
                if (!string.IsNullOrEmpty(step.BackupPath) && File.Exists(step.BackupPath))
                {
                    File.Copy(step.BackupPath, step.TargetPath, overwrite: true);
                    if (!string.IsNullOrEmpty(step.OriginalHash))
                    {
                        string restoredHash = PackageManifest.ComputeFileSha256(step.TargetPath);
                        if (!string.Equals(restoredHash, step.OriginalHash, StringComparison.OrdinalIgnoreCase))
                        {
                            InstallerLogger.LogErr($"Restored file SHA-256 mismatch! Expected {step.OriginalHash}, got {restoredHash}");
                            return false;
                        }
                    }
                }
                return true;

            case "ReplaceFile":
                // Restore backup -> target
                if (!string.IsNullOrEmpty(step.BackupPath) && File.Exists(step.BackupPath))
                {
                    File.Copy(step.BackupPath, step.TargetPath, overwrite: true);
                    if (!string.IsNullOrEmpty(step.OriginalHash))
                    {
                        string restoredHash = PackageManifest.ComputeFileSha256(step.TargetPath);
                        if (!string.Equals(restoredHash, step.OriginalHash, StringComparison.OrdinalIgnoreCase))
                        {
                            InstallerLogger.LogErr($"Restored file SHA-256 mismatch! Expected {step.OriginalHash}, got {restoredHash}");
                            return false;
                        }
                    }
                    return true;
                }
                return false;

            case "DeleteFile":
                // Restore deleted file from backup
                if (!string.IsNullOrEmpty(step.BackupPath) && File.Exists(step.BackupPath))
                {
                    string? dir = Path.GetDirectoryName(step.TargetPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    File.Copy(step.BackupPath, step.TargetPath, overwrite: true);
                    if (!string.IsNullOrEmpty(step.OriginalHash))
                    {
                        string restoredHash = PackageManifest.ComputeFileSha256(step.TargetPath);
                        if (!string.Equals(restoredHash, step.OriginalHash, StringComparison.OrdinalIgnoreCase))
                        {
                            InstallerLogger.LogErr($"Restored deleted file SHA-256 mismatch! Expected {step.OriginalHash}, got {restoredHash}");
                            return false;
                        }
                    }
                    return true;
                }
                return false;

            case "CreateDir":
                if (Directory.Exists(step.TargetPath))
                {
                    if (!Directory.EnumerateFileSystemEntries(step.TargetPath).Any())
                    {
                        Directory.Delete(step.TargetPath, recursive: false);
                    }
                }
                return true;

            case "SaveStateFile":
                if (!string.IsNullOrEmpty(step.BackupPath) && File.Exists(step.BackupPath))
                {
                    File.Copy(step.BackupPath, step.TargetPath, overwrite: true);
                    if (!string.IsNullOrEmpty(step.OriginalHash))
                    {
                        string restoredHash = PackageManifest.ComputeFileSha256(step.TargetPath);
                        if (!string.Equals(restoredHash, step.OriginalHash, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                }
                else if (File.Exists(step.TargetPath))
                {
                    // No prior state existed, remove created file
                    File.Delete(step.TargetPath);
                }
                return true;

            case "CreateThirdPartyBackup":
                if (!string.IsNullOrEmpty(step.BackupPath) && File.Exists(step.BackupPath))
                {
                    // If target is missing, restore
                    if (!File.Exists(step.TargetPath))
                    {
                        File.Copy(step.BackupPath, step.TargetPath, overwrite: true);
                    }

                    // If target matches original hash, safe to delete redundant persistent backup
                    if (File.Exists(step.TargetPath) && !string.IsNullOrEmpty(step.OriginalHash))
                    {
                        string currentHash = PackageManifest.ComputeFileSha256(step.TargetPath);
                        if (string.Equals(currentHash, step.OriginalHash, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(step.BackupPath);
                        }
                    }
                }
                return true;

            default:
                InstallerLogger.LogWarn($"Unknown transaction operation during rollback: {step.Operation}");
                return true;
        }
    }
}
