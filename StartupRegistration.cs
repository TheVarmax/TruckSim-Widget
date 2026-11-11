using System;
using System.IO;
using Microsoft.Win32;

namespace ETSOverlay
{
    /// <summary>How the application was started.</summary>
    internal enum LaunchReason
    {
        /// <summary>Shortcut, Start menu, installer, double click.</summary>
        Manual,
        /// <summary>Windows sign-in (the Run entry).</summary>
        Autostart,
        /// <summary>Restart after an update.</summary>
        Updated
    }

    internal static class LaunchContext
    {
        public const string AutostartArgument = "--autostart";
        public const string UpdatedArgument = "--updated";

        public static LaunchReason Current { get; private set; } = LaunchReason.Manual;

        public static LaunchReason Parse(string[]? args)
        {
            if (args == null) return LaunchReason.Manual;
            bool autostart = false;
            foreach (var raw in args)
            {
                string arg = (raw ?? "").Trim();
                // An update restart is always a visible start, whatever else is on the command line.
                if (string.Equals(arg, UpdatedArgument, StringComparison.OrdinalIgnoreCase)) return LaunchReason.Updated;
                if (string.Equals(arg, AutostartArgument, StringComparison.OrdinalIgnoreCase)) autostart = true;
            }
            return autostart ? LaunchReason.Autostart : LaunchReason.Manual;
        }

        public static void Initialize(string[]? args) => Current = Parse(args);
    }

    internal enum StartupEntryState
    {
        /// <summary>No Run entry.</summary>
        Missing,
        /// <summary>The entry exists and Windows runs it at sign-in.</summary>
        Enabled,
        /// <summary>The entry exists, but the user switched it off in Windows (Task Manager / Settings).</summary>
        DisabledByWindows,
        /// <summary>A policy stops Windows from processing the per-user Run list.</summary>
        BlockedByPolicy
    }

    internal sealed class StartupStatus
    {
        public StartupEntryState State { get; init; }
        /// <summary>The entry starts this executable with the autostart argument.</summary>
        public bool PointsToThisExecutable { get; init; }
        public string Error { get; init; } = "";
        public bool WillRunAtSignIn => State == StartupEntryState.Enabled && PointsToThisExecutable;
    }

    /// <summary>Registry access used by the startup registration (replaceable in tests).</summary>
    internal interface IStartupRegistry
    {
        string? GetRunCommand(string valueName);
        void SetRunCommand(string valueName, string command);
        void DeleteRunCommand(string valueName);
        byte[]? GetStartupApproved(string valueName);
        void DeleteStartupApproved(string valueName);
        bool IsRunListBlockedByPolicy();
    }

    internal sealed class CurrentUserStartupRegistry : IStartupRegistry
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string PolicyKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";

        public string? GetRunCommand(string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(valueName) as string;
        }

        public void SetRunCommand(string valueName, string command)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey)
                ?? throw new InvalidOperationException("The Run key is not available.");
            key.SetValue(valueName, command, RegistryValueKind.String);
        }

        public void DeleteRunCommand(string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }

        public byte[]? GetStartupApproved(string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            return key?.GetValue(valueName) as byte[];
        }

        public void DeleteStartupApproved(string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }

        public bool IsRunListBlockedByPolicy()
        {
            return ReadPolicy(Registry.CurrentUser) || ReadPolicy(Registry.LocalMachine);
        }

        private static bool ReadPolicy(RegistryKey hive)
        {
            try
            {
                using var key = hive.OpenSubKey(PolicyKey);
                return key?.GetValue("DisableCurrentUserRun") is int value && value != 0;
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// Start at Windows sign-in for the current user, without elevation: one value in
    /// HKCU\...\Run with the quoted absolute path and the autostart argument.
    /// The installer writes and removes the same value (TruckSimWidgetSetup AutostartRegistration).
    /// The "disabled" switch of Windows (StartupApproved) is never changed here, so neither a
    /// normal start nor an update can undo what the user switched off in Windows.
    /// </summary>
    internal static class StartupRegistration
    {
        /// <summary>Name of the Run value. Must stay equal to the installer's constant.</summary>
        public const string ValueName = "TruckSim Widget";
        /// <summary>Windows does not run longer Run commands.</summary>
        public const int MaxCommandLength = 260;

        public static bool TryBuildCommand(string? executablePath, out string command, out string error)
        {
            command = "";
            error = "";
            if (string.IsNullOrWhiteSpace(executablePath)) { error = "The executable path is empty."; return false; }
            if (executablePath.IndexOf('"') >= 0) { error = "The executable path contains a quote."; return false; }
            if (!Path.IsPathFullyQualified(executablePath)) { error = "The executable path is not absolute."; return false; }
            if (!executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { error = "The path is not an executable."; return false; }

            command = $"\"{executablePath}\" {LaunchContext.AutostartArgument}";
            if (command.Length > MaxCommandLength)
            {
                command = "";
                error = "The startup command is longer than Windows allows.";
                return false;
            }
            return true;
        }

        /// <summary>Path of the executable a Run command starts, or "" when it cannot be read.</summary>
        public static string ExtractExecutablePath(string? command)
        {
            if (string.IsNullOrWhiteSpace(command)) return "";
            string text = command.Trim();
            if (text.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = text.IndexOf('"', 1);
                return end > 1 ? text.Substring(1, end - 1) : "";
            }
            int exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return exe > 0 ? text.Substring(0, exe + 4) : text;
        }

        public static bool IsDisabledByWindows(byte[]? startupApproved)
        {
            // First byte: 02/06 = enabled, 03/07 = disabled by the user.
            return startupApproved != null && startupApproved.Length > 0 && (startupApproved[0] & 1) == 1;
        }

        public static StartupStatus GetStatus(IStartupRegistry registry, string? executablePath)
        {
            try
            {
                string? command = registry.GetRunCommand(ValueName);
                if (string.IsNullOrWhiteSpace(command))
                    return new StartupStatus { State = StartupEntryState.Missing };

                bool pointsHere = TryBuildCommand(executablePath, out string expected, out _)
                    && string.Equals(command.Trim(), expected, StringComparison.OrdinalIgnoreCase);

                StartupEntryState state = StartupEntryState.Enabled;
                if (registry.IsRunListBlockedByPolicy()) state = StartupEntryState.BlockedByPolicy;
                else if (IsDisabledByWindows(registry.GetStartupApproved(ValueName))) state = StartupEntryState.DisabledByWindows;

                return new StartupStatus { State = state, PointsToThisExecutable = pointsHere };
            }
            catch (Exception ex)
            {
                return new StartupStatus { State = StartupEntryState.Missing, Error = ex.Message };
            }
        }

        /// <summary>Creates the entry or points it at this executable. Windows' own on/off state is kept.</summary>
        public static StartupStatus Enable(IStartupRegistry registry, string? executablePath)
        {
            if (!TryBuildCommand(executablePath, out string command, out string error))
                return new StartupStatus { State = SafeState(registry), Error = error };
            try
            {
                string? current = registry.GetRunCommand(ValueName);
                if (!string.Equals(current, command, StringComparison.Ordinal))
                    registry.SetRunCommand(ValueName, command);
                return GetStatus(registry, executablePath);
            }
            catch (Exception ex)
            {
                return new StartupStatus { State = SafeState(registry), Error = ex.Message };
            }
        }

        /// <summary>Removes the entry together with its Windows on/off record.</summary>
        public static StartupStatus Disable(IStartupRegistry registry)
        {
            try
            {
                registry.DeleteRunCommand(ValueName);
                registry.DeleteStartupApproved(ValueName);
                return new StartupStatus { State = StartupEntryState.Missing };
            }
            catch (Exception ex)
            {
                return new StartupStatus { State = SafeState(registry), Error = ex.Message };
            }
        }

        /// <summary>
        /// Brings the entry in line with the saved choice when the application starts.
        /// Off removes a leftover entry. On repairs an entry whose file is gone (the install path
        /// changed). An entry that starts another existing copy of the widget is left alone: a
        /// second copy (a development build, a portable folder) must not take over the entry of
        /// the installed one just by being started. A missing entry is created only when the
        /// default is applied for the first time: an entry the user removed is not put back by a
        /// normal start.
        /// </summary>
        /// <param name="fileExists">File check, replaceable in tests.</param>
        public static StartupStatus Reconcile(IStartupRegistry registry, string? executablePath, bool wanted, bool createIfMissing, Func<string, bool>? fileExists = null)
        {
            fileExists ??= File.Exists;
            try
            {
                string? current = registry.GetRunCommand(ValueName);
                bool exists = !string.IsNullOrWhiteSpace(current);

                if (!wanted)
                    return exists ? Disable(registry) : new StartupStatus { State = StartupEntryState.Missing };

                if (exists)
                {
                    string target = ExtractExecutablePath(current);
                    bool pointsHere = TryBuildCommand(executablePath, out string expected, out _)
                        && string.Equals(current!.Trim(), expected, StringComparison.OrdinalIgnoreCase);
                    if (pointsHere || (target.Length > 0 && fileExists(target)))
                        return GetStatus(registry, executablePath);
                    return Enable(registry, executablePath);
                }

                if (createIfMissing)
                    return Enable(registry, executablePath);

                return new StartupStatus { State = StartupEntryState.Missing };
            }
            catch (Exception ex)
            {
                return new StartupStatus { State = StartupEntryState.Missing, Error = ex.Message };
            }
        }

        private static StartupEntryState SafeState(IStartupRegistry registry)
        {
            try { return GetStatus(registry, null).State; }
            catch { return StartupEntryState.Missing; }
        }
    }

    /// <summary>What automatic tray behaviour does.</summary>
    internal enum TrayAction
    {
        None,
        Show,
        Hide
    }

    internal sealed class TraySettings
    {
        public bool AutostartEnabled { get; init; }
        public bool StartInTray { get; init; }
        public bool ShowOnGameStart { get; init; }
        public bool HideOnGameExit { get; init; }

        /// <summary>Both game options need autostart and start in tray.</summary>
    }

    /// <summary>
    /// Decides when the widget is shown or hidden by itself. It reacts only to a confirmed change
    /// of "a game process runs / no game process runs". Repeated checks of the same state, a
    /// licence refresh, a pause, a menu or lost telemetry never produce an action, so whatever the
    /// user did by hand stays until the next real change.
    /// </summary>
    internal sealed class TrayAutomationPolicy
    {
        /// <summary>Checks without any game process before the games count as closed.</summary>
        public const int CloseConfirmChecks = 2;

        private bool? _gameRunning;
        private int _missingChecks;

        public bool? GameRunning => _gameRunning;

        public static bool ShouldStartHidden(LaunchReason reason, TraySettings settings, bool trayAvailable)
        {
            return reason == LaunchReason.Autostart && settings.AutostartEnabled && settings.StartInTray && trayAvailable;
        }

        /// <param name="gameProcessRunning">ETS2 or ATS process exists.</param>
        /// <param name="entitled">The licence right for the two game options is confirmed right now.</param>
        /// <param name="hiddenInTray">Current visibility of the widget.</param>
        public TrayAction OnProcessCheck(bool gameProcessRunning, TraySettings settings, bool entitled, bool hiddenInTray)
        {
            bool canAct = entitled;

            if (_gameRunning == null)
            {
                // First check after the widget started: a game that already runs counts as started once.
                _gameRunning = gameProcessRunning;
                _missingChecks = 0;
                return gameProcessRunning && canAct && settings.ShowOnGameStart && hiddenInTray ? TrayAction.Show : TrayAction.None;
            }

            if (gameProcessRunning)
            {
                _missingChecks = 0;
                if (_gameRunning == true) return TrayAction.None;
                _gameRunning = true;
                return canAct && settings.ShowOnGameStart && hiddenInTray ? TrayAction.Show : TrayAction.None;
            }

            if (_gameRunning == false) return TrayAction.None;

            // One check without a process is not yet "closed" (ETS2 handing over to ATS, a slow list).
            _missingChecks++;
            if (_missingChecks < CloseConfirmChecks) return TrayAction.None;

            _gameRunning = false;
            _missingChecks = 0;
            return canAct && settings.HideOnGameExit && !hiddenInTray ? TrayAction.Hide : TrayAction.None;
        }
    }
}
