using System.Text.Json;
using Microsoft.Win32;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;
using TruckSimWidgetSetup.TransactionEngine;

namespace TruckSimWidgetSetup.InstallerCore;

/// <summary>Registry access of the startup entry (replaceable in tests).</summary>
public interface IAutostartRegistry
{
    string? GetRunCommand();
    void SetRunCommand(string command);
    void DeleteRunCommand();
    /// <summary>The Windows on/off record of the entry (Task Manager / Settings).</summary>
    byte[]? GetStartupApproved();
    void SetStartupApproved(byte[] value);
    void DeleteStartupApproved();
}

internal sealed class CurrentUserAutostartRegistry : IAutostartRegistry
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public string? GetRunCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AutostartRegistration.ValueName) as string;
    }

    public void SetRunCommand(string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey)
            ?? throw new InvalidOperationException("The Run key is not available.");
        key.SetValue(AutostartRegistration.ValueName, command, RegistryValueKind.String);
    }

    public void DeleteRunCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(AutostartRegistration.ValueName, throwOnMissingValue: false);
    }

    public byte[]? GetStartupApproved()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey);
        return key?.GetValue(AutostartRegistration.ValueName) as byte[];
    }

    public void SetStartupApproved(byte[] value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(ApprovedKey)
            ?? throw new InvalidOperationException("The StartupApproved key is not available.");
        key.SetValue(AutostartRegistration.ValueName, value, RegistryValueKind.Binary);
    }

    public void DeleteStartupApproved()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        key?.DeleteValue(AutostartRegistration.ValueName, throwOnMissingValue: false);
    }
}

/// <summary>
/// Start of the widget at Windows sign-in for the user who installs it (no elevation): one value
/// in HKCU\...\Run. The application reads and writes the same value (StartupRegistration in the
/// widget), so both sides must keep the value name and the argument equal.
///
/// Every change is a step of the installer transaction, made before the commit and recorded in
/// the journal with the previous value. If the install, update or uninstall fails, is rolled back
/// or is interrupted and recovered later, the entry returns to what it was, together with the
/// files it points at. On install and update the Windows on/off record (StartupApproved) is never
/// changed: an update cannot switch back on what the user switched off in Windows.
/// </summary>
public static class AutostartRegistration
{
    /// <summary>Name of the Run value. Equal to StartupRegistration.ValueName in the widget.</summary>
    public const string ValueName = "TruckSim Widget";
    /// <summary>Equal to LaunchContext.AutostartArgument in the widget.</summary>
    public const string AutostartArgument = "--autostart";
    /// <summary>Windows does not run longer Run commands.</summary>
    public const int MaxCommandLength = 260;

    /// <summary>Journal step: the entry was written or removed during install/update/repair.</summary>
    public const string StepChange = "ChangeAutostart";
    /// <summary>Journal step: the entry was removed during uninstall.</summary>
    public const string StepRemove = "RemoveAutostart";

    private const string WasPresent = "present";
    private const string WasAbsent = "absent";

    /// <summary>Registry used by the installer; tests replace it.</summary>
    internal static IAutostartRegistry Registry { get; set; } = new CurrentUserAutostartRegistry();

    public enum Change
    {
        None,
        Write,
        Remove
    }

    /// <summary>Run command for the installed widget, or null when it cannot be a valid command.</summary>
    public static string? BuildCommand(string appDir)
    {
        if (string.IsNullOrWhiteSpace(appDir) || appDir.Contains('"')) return null;
        string exePath = Path.Combine(appDir, Constants.AppExeName);
        if (!Path.IsPathFullyQualified(exePath)) return null;
        string command = $"\"{exePath}\" {AutostartArgument}";
        return command.Length <= MaxCommandLength ? command : null;
    }

    /// <summary>
    /// What to do with the entry when files are installed.
    /// </summary>
    /// <param name="existingCommand">Current Run value, or null.</param>
    /// <param name="expectedCommand">Command for the new install path, or null when invalid.</param>
    /// <param name="savedChoice">The user's saved choice from the widget state: true, false, or null when never chosen.</param>
    /// <param name="cleanInstall">The product was not installed before this run.</param>
    public static Change Decide(string? existingCommand, string? expectedCommand, bool? savedChoice, bool cleanInstall)
    {
        bool exists = !string.IsNullOrWhiteSpace(existingCommand);

        // An explicit "Off" survives updates, repairs and reinstalls.
        if (savedChoice == false) return exists ? Change.Remove : Change.None;

        if (expectedCommand == null) return Change.None;

        if (exists)
        {
            // Keep an existing entry pointing at the installed file (the install path may have changed).
            return string.Equals(existingCommand!.Trim(), expectedCommand, StringComparison.OrdinalIgnoreCase)
                ? Change.None
                : Change.Write;
        }

        // No entry: only a clean install creates it. On an update the widget applies the default
        // once itself, and an entry the user removed is not put back.
        return cleanInstall ? Change.Write : Change.None;
    }

    /// <summary>
    /// Reads the user's choice from the widget state file. Null when the file is missing, is not
    /// JSON, or was written by a version without the startup settings.
    /// </summary>
    public static bool? ReadSavedChoice(string stateFilePath)
    {
        try
        {
            if (!File.Exists(stateFilePath)) return null;
            string content = File.ReadAllText(stateFilePath);
            if (!content.TrimStart().StartsWith("{", StringComparison.Ordinal)) return null;

            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (!root.TryGetProperty("StartupSettingsVersion", out var version)
                || version.ValueKind != JsonValueKind.Number || version.GetInt32() < 1)
                return null;
            if (!root.TryGetProperty("AutostartEnabled", out var enabled)) return null;
            return enabled.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Install, update or repair: brings the entry in line with the new install path inside the
    /// transaction. A registry error is logged and does not fail the installation.
    /// </summary>
    public static void ApplyDuringInstall(TransactionJournal journal, string appDir, bool cleanInstall, string? widgetStateFilePath = null)
    {
        try
        {
            string? expected = BuildCommand(appDir);
            if (expected == null)
                InstallerLogger.LogWarn("Autostart: the install path does not give a valid startup command; the entry is left as it is.");

            bool? savedChoice = ReadSavedChoice(widgetStateFilePath ?? Path.Combine(Constants.GetUserDataDir(), "state.dat"));
            string? existing = Registry.GetRunCommand();
            Change change = Decide(existing, expected, savedChoice, cleanInstall);
            if (change == Change.None)
            {
                InstallerLogger.LogInfo("Autostart: entry left unchanged.");
                return;
            }

            // The previous value goes into the journal before anything is changed.
            byte[]? approved = change == Change.Remove ? Registry.GetStartupApproved() : null;
            int step = BeginStep(journal, StepChange, existing, approved);

            if (change == Change.Write)
            {
                Registry.SetRunCommand(expected!);
                InstallerLogger.LogInfo($"Autostart: entry written for the current user ({(cleanInstall ? "clean install" : "path refreshed")}).");
            }
            else
            {
                Registry.DeleteRunCommand();
                Registry.DeleteStartupApproved();
                InstallerLogger.LogInfo("Autostart: entry removed (switched off in the widget settings).");
            }

            journal.CompleteStep(step, change == Change.Write ? expected : null);
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Autostart: could not update the startup entry: {ex.Message}");
        }
    }

    /// <summary>
    /// Uninstall: removes the product's own entry inside the transaction, also when user data is
    /// kept. If the uninstall is rolled back, the entry and its Windows on/off record come back.
    /// </summary>
    public static void RemoveDuringUninstall(TransactionJournal journal)
    {
        try
        {
            string? existing = Registry.GetRunCommand();
            byte[]? approved = Registry.GetStartupApproved();
            if (existing == null && approved == null) return;

            int step = BeginStep(journal, StepRemove, existing, approved);
            Registry.DeleteRunCommand();
            Registry.DeleteStartupApproved();
            journal.CompleteStep(step);
            InstallerLogger.LogInfo("Autostart: startup entry removed.");
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Autostart: could not remove the startup entry: {ex.Message}");
        }
    }

    private static int BeginStep(TransactionJournal journal, string operation, string? previousCommand, byte[]? previousApproved)
    {
        return journal.BeginStep(
            operation,
            previousCommand ?? string.Empty,
            ValueName,
            previousApproved != null ? Convert.ToHexString(previousApproved) : string.Empty,
            previousCommand != null ? WasPresent : WasAbsent);
    }

    /// <summary>
    /// Puts the entry back to the state recorded in the journal step. Safe for a step that was
    /// interrupted half-way and safe to repeat.
    /// </summary>
    internal static bool Rollback(TransactionStep step)
    {
        try
        {
            if (step.OriginalHash == WasPresent && !string.IsNullOrEmpty(step.SourcePath))
                Registry.SetRunCommand(step.SourcePath);
            else
                Registry.DeleteRunCommand();

            // Only a step that removed the Windows on/off record recorded it; restore it then.
            if (!string.IsNullOrEmpty(step.BackupPath))
                Registry.SetStartupApproved(Convert.FromHexString(step.BackupPath));

            InstallerLogger.LogInfo("Autostart: startup entry restored by rollback.");
            return true;
        }
        catch (Exception ex)
        {
            InstallerLogger.LogErr($"Autostart: could not restore the startup entry: {ex.Message}");
            return false;
        }
    }
}
