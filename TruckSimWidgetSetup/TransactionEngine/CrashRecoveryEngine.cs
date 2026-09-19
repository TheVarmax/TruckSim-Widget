using System.Text.Json;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.TransactionEngine;

public static class CrashRecoveryEngine
{
    public static bool CheckAndExecuteRecovery(string? customJournalPath = null)
    {
        string journalPath = customJournalPath ?? Constants.GetTransactionJournalFilePath();

        if (!File.Exists(journalPath))
        {
            // Clean orphaned staging if any
            string stagingDir = Constants.GetTransactionStagingDir();
            try
            {
                if (Directory.Exists(stagingDir))
                {
                    Directory.Delete(stagingDir, recursive: true);
                }
            }
            catch { }
            return true;
        }

        InstallerLogger.LogWarn($"Found existing transaction journal at: {journalPath}");

        TransactionJournal? journal;
        try
        {
            string json = File.ReadAllText(journalPath);
            journal = JsonSerializer.Deserialize<TransactionJournal>(json, TransactionJournal.JsonOptions);
        }
        catch (Exception ex)
        {
            InstallerLogger.LogErr($"Transaction journal is corrupted or unparseable: {ex.Message}. Halting automated recovery for safety.");
            return false;
        }

        if (journal == null || string.IsNullOrEmpty(journal.TransactionId))
        {
            InstallerLogger.LogErr("Transaction journal has invalid schema. Preserving files for diagnostics.");
            return false;
        }

        journal.JournalFilePath = journalPath;

        if (journal.Status == "PENDING")
        {
            InstallerLogger.LogWarn($"Incomplete PENDING transaction ({journal.TransactionId}) detected! Executing automatic crash recovery.");
            bool rollbackOk = journal.Rollback();

            if (rollbackOk)
            {
                InstallerLogger.LogInfo("Crash recovery completed and verified successfully.");
                try
                {
                    File.Delete(journalPath);
                }
                catch { }
                return true;
            }
            else
            {
                InstallerLogger.LogErr("Crash recovery was unable to verify all steps. Preserving journal and staging for manual inspection.");
                return false;
            }
        }
        else
        {
            // Stale COMMITTED or ROLLED_BACK journal
            InstallerLogger.LogInfo($"Cleaning up stale journal with status '{journal.Status}'.");
            try
            {
                string stagingDir = Constants.GetTransactionStagingDir();
                if (Directory.Exists(stagingDir))
                {
                    Directory.Delete(stagingDir, recursive: true);
                }
                File.Delete(journalPath);
            }
            catch { }
            return true;
        }
    }
}
