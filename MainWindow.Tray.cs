using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ETSOverlay
{
    // Start at sign-in, the tray icon, window visibility and the single shutdown path.
    // Telemetry handlers do not show or hide windows: they only report the process state
    // (EvaluateTrayAutomation) and everything visible is decided here.
    public partial class MainWindow
    {
        /// <summary>Licence feature that allows showing/hiding the widget with the game (Supporter and above).</summary>
        internal const string TrayAutomationFeature = "tray_automation";
        private const int StartupSettingsCurrentVersion = 1;

        // Saved on this PC only (never synced to the cloud)
        private int _startupSettingsVersion = 0;
        private bool _autostartEnabled = true;
        private bool _startInTray = false;
        private bool _showOnGameStart = false;
        private bool _hideOnGameExit = false;

        // State of the current run only
        private TrayIconService? _tray;
        private bool _hiddenInTray = false;
        private bool _trayReady = false;
        private bool _shutdownStarted = false;
        private Action? _pendingWhenShown;
        private readonly TrayAutomationPolicy _trayPolicy = new();
        private readonly IStartupRegistry _startupRegistry = new CurrentUserStartupRegistry();
        private StartupStatus _startupStatus = new() { State = StartupEntryState.Missing };

        public bool IsHiddenInTray => _hiddenInTray;
        public bool AutostartEnabled => _autostartEnabled;
        public bool StartInTray => _startInTray;
        public bool ShowOnGameStart => _showOnGameStart;
        public bool HideOnGameExit => _hideOnGameExit;

        /// <summary>
        /// The right for the two game options, checked every time it is used: a valid licence that
        /// the server confirmed, not expired, with the feature. Works offline only from such a
        /// confirmed licence.
        /// </summary>
        public bool CanUseGameTrayAutomation => LicenseManager.Instance.HasEntitlement(TrayAutomationFeature);

        private TraySettings CurrentTraySettings => new()
        {
            AutostartEnabled = _autostartEnabled,
            StartInTray = _startInTray,
            ShowOnGameStart = _showOnGameStart,
            HideOnGameExit = _hideOnGameExit
        };

        // ---------- startup ----------

        /// <summary>Creates the tray icon. Called before the saved state is applied, so a start in the tray never shows a window first.</summary>
        private void InitializeTrayIcon()
        {
            try
            {
                _tray = new TrayIconService(WriteLog)
                {
                    MenuText = () => (
                        _hiddenInTray
                            ? (uiLanguage == "uk" ? "Показати віджет" : "Show widget")
                            : (uiLanguage == "uk" ? "Згорнути в трей" : "Minimize to tray"),
                        uiLanguage == "uk" ? "Вихід" : "Exit")
                };
                _tray.ShowRequested += () => RestoreFromTray("tray double click");
                _tray.ToggleRequested += () =>
                {
                    if (_hiddenInTray) RestoreFromTray("tray menu");
                    else HideToTray("tray menu");
                };
                _tray.ExitRequested += () => ShutdownApplication("user_exit");
                _tray.IconUnavailable += () =>
                {
                    // Without the icon a hidden widget could not be reached any more.
                    if (_hiddenInTray) RestoreFromTray("tray icon unavailable");
                };
                if (!_tray.EnsureIcon()) WriteLog("[TRAY] Icon unavailable at startup; the widget stays visible.");
            }
            catch (Exception ex)
            {
                WriteLog($"[TRAY] Initialization failed: {ex.Message}");
                _tray = null;
            }
        }

        /// <summary>Decides, from the saved settings and the launch reason, whether this run starts in the tray.</summary>
        private void ApplyStartupVisibility()
        {
            bool trayAvailable = _tray != null && _tray.IsAvailable;
            // Settings of a version that had no autostart are the defaults: never start hidden from them.
            bool settingsKnown = _startupSettingsVersion >= StartupSettingsCurrentVersion;
            _hiddenInTray = settingsKnown && TrayAutomationPolicy.ShouldStartHidden(LaunchContext.Current, CurrentTraySettings, trayAvailable);
            if (_hiddenInTray)
            {
                // Hiding before WPF shows the startup window means it is never shown: no flash,
                // no intro animation and no focus change.
                _skipStartupAnimation = true;
                Hide();
                WriteLog("[TRAY] Started at sign-in in the tray.");
            }
            else if (LaunchContext.Current == LaunchReason.Autostart)
            {
                WriteLog("[TRAY] Started at sign-in with the widget visible.");
            }
        }

        /// <summary>
        /// Applies the defaults once when the saved state has no startup settings yet (first run or
        /// an update from an older version) and keeps the Windows entry in line with the choice.
        /// </summary>
        private void ApplyStartupRegistrationOnLaunch()
        {
            bool firstTime = _startupSettingsVersion < StartupSettingsCurrentVersion;
            bool createIfMissing = firstTime;
#if DEBUG
            // A development build is not registered to start with Windows by itself, and it does
            // not use up the one-time default: the installed build still applies it later.
            createIfMissing = false;
#endif
            if (firstTime)
            {
                _autostartEnabled = true;
                _startInTray = false;
                _showOnGameStart = false;
                _hideOnGameExit = false;
                if (createIfMissing) _startupSettingsVersion = StartupSettingsCurrentVersion;
            }
            try
            {
#if DEBUG
                // Read only: starting a development build never writes, rewrites or removes the
                // startup entry (it may belong to the installed widget). The switch in the
                // settings still works, because that is an explicit action.
                _startupStatus = StartupRegistration.GetStatus(_startupRegistry, Environment.ProcessPath);
#else
                _startupStatus = StartupRegistration.Reconcile(_startupRegistry, Environment.ProcessPath, _autostartEnabled, createIfMissing);
#endif
                WriteLog($"[AUTOSTART] wanted={_autostartEnabled} firstTime={firstTime} state={_startupStatus.State} thisExe={_startupStatus.PointsToThisExecutable}" +
                    (string.IsNullOrEmpty(_startupStatus.Error) ? "" : $" error={_startupStatus.Error}"));
            }
            catch (Exception ex)
            {
                WriteLog($"[AUTOSTART] Reconcile failed: {ex.Message}");
            }

            if (firstTime && createIfMissing) SaveState();
        }

        // ---------- settings ----------

        /// <summary>Actual state of the Windows entry (not the saved wish).</summary>
        internal StartupStatus GetStartupStatus()
        {
            _startupStatus = StartupRegistration.GetStatus(_startupRegistry, Environment.ProcessPath);
            return _startupStatus;
        }

        /// <summary>Switches start at sign-in on or off. The returned status tells what really happened.</summary>
        internal StartupStatus SetAutostartEnabled(bool enabled)
        {
            _autostartEnabled = enabled;
            _startupSettingsVersion = StartupSettingsCurrentVersion; // an explicit choice from now on
            _startupStatus = enabled
                ? StartupRegistration.Enable(_startupRegistry, Environment.ProcessPath)
                : StartupRegistration.Disable(_startupRegistry);
            WriteLog($"[AUTOSTART] set={enabled} state={_startupStatus.State} thisExe={_startupStatus.PointsToThisExecutable}" +
                (string.IsNullOrEmpty(_startupStatus.Error) ? "" : $" error={_startupStatus.Error}"));
            SaveState();
            return _startupStatus;
        }

        // Windows startup preferences and game automation are saved independently.
        // The licence right is checked when an automatic game action is performed.
        public void SetStartInTray(bool enabled) { _startInTray = enabled; SaveStartupChoice(); }
        public void SetShowOnGameStart(bool enabled) { _showOnGameStart = enabled; SaveStartupChoice(); }
        public void SetHideOnGameExit(bool enabled) { _hideOnGameExit = enabled; SaveStartupChoice(); }

        private void SaveStartupChoice()
        {
            _startupSettingsVersion = StartupSettingsCurrentVersion; // an explicit choice from now on
            SaveState();
        }

        // ---------- visibility ----------

        /// <summary>
        /// Hides the widget, the HUD, the header and the settings windows. Tracking, telemetry and
        /// saving keep running. Returns false (and hides nothing) when there is no tray icon to
        /// bring the widget back with.
        /// </summary>
        public bool HideToTray(string reason)
        {
            if (_hiddenInTray) return true;
            if (_tray == null || !_tray.EnsureIcon())
            {
                WriteLog($"[TRAY] Not hidden ({reason}): the tray icon is unavailable.");
                return false;
            }

            _hiddenInTray = true;
            WriteLog($"[TRAY] Hidden to tray ({reason}).");
            try
            {
                // TrucksBook auto-hide works on a visible widget only.
                StopIdleTimer();
                ExitIdleState();
                SaveState();

                _settingsWindow?.CloseTransientPanels();
                _settingsWindow?.Hide();
                _wasSettingsVisibleBeforeMinimize = false;
                HideHeaderOverlay();
                _headerOverlay?.Hide();
                _hudSyncTimer?.Stop();
                _hudWindow?.Hide();
                Hide();
            }
            catch (Exception ex) { WriteLog($"[TRAY] Hide failed: {ex.Message}"); }
            return true;
        }

        /// <summary>
        /// Shows the widget in the mode that is allowed now, at its saved position inside the
        /// screen. The settings window is not reopened. The TrucksBook auto-hide countdown starts over.
        /// </summary>
        public void RestoreFromTray(string reason)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => RestoreFromTray(reason)));
                return;
            }

            try
            {
                if (!_hiddenInTray)
                {
                    // Already visible (e.g. the shortcut was used again): bring it to the front.
                    if (_uiMode == "hud" && _hudWindow != null)
                    {
                        if (_hudWindow.WindowState == WindowState.Minimized) _hudWindow.WindowState = WindowState.Normal;
                        _hudWindow.Activate();
                    }
                    else
                    {
                        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                        if (IsVisible) Activate();
                    }
                    return;
                }

                _hiddenInTray = false;
                WriteLog($"[TRAY] Restored from tray ({reason}), mode={_uiMode}.");

                if (_uiMode == "hud")
                {
                    ApplyUIMode(false);
                    _hudWindow?.Activate();
                }
                else
                {
                    if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                    Show();
                    FitRestoredWindow(this, () => WindowGuard.CenterOnPrimary(this), "main");
                    EnsureHeaderOverlay();
                    _headerOverlay?.Show();
                    UpdateHeaderOverlayPosition();
                    Activate();
                }

                // The window is shown first; only then the regular auto-hide timer starts from zero.
                StopIdleTimer();
                ExitIdleState();
                if (_autoHideEnabled && _startupComplete && !_mainBorderHovered) StartIdleTimer();

                var pending = _pendingWhenShown;
                _pendingWhenShown = null;
                if (pending != null) Dispatcher.BeginInvoke(pending, DispatcherPriority.ApplicationIdle);
            }
            catch (Exception ex) { WriteLog($"[TRAY] Restore failed: {ex.Message}"); }
        }

        /// <summary>"Minimize to tray" command of the header and the HUD.</summary>
        public void BtnTray_Click(object? sender, RoutedEventArgs e)
        {
            HideToTray("user command");
        }

        public string GetTrayCommandText() => uiLanguage == "uk" ? "Згорнути в трей" : "Minimize to tray";

        /// <summary>
        /// Called on every process check with the confirmed "ETS2 or ATS runs" state. Shows the
        /// widget when the first game appears and hides it after the last one closes, when the
        /// options are on and the licence right is confirmed at that moment.
        /// </summary>
        private void EvaluateTrayAutomation(bool gameProcessRunning)
        {
            if (!_trayReady || _shutdownStarted) return;
            TrayAction action;
            try
            {
                action = _trayPolicy.OnProcessCheck(gameProcessRunning, CurrentTraySettings, CanUseGameTrayAutomation, _hiddenInTray);
            }
            catch (Exception ex)
            {
                WriteLog($"[TRAY] Automation check failed: {ex.Message}");
                return;
            }

            // Run after the current status check, not in the middle of it.
            if (action == TrayAction.Show)
                Dispatcher.BeginInvoke(new Action(() => RestoreFromTray("game started")));
            else if (action == TrayAction.Hide)
                Dispatcher.BeginInvoke(new Action(() => HideToTray("games closed")));
        }

        // ---------- shutdown ----------

        /// <summary>
        /// The one way the application ends: saves the state, releases the brake, stops timers and
        /// telemetry, removes the tray icon. Used for Exit, the close button, the end of the
        /// Windows session and closing for an update. Safe to call more than once.
        /// </summary>
        /// <param name="reason">"user_exit", "system_shutdown" or "update".</param>
        /// <param name="exitProcess">False when Windows is ending the session and closes the process itself.</param>
        internal void ShutdownApplication(string reason, bool exitProcess = true)
        {
            if (_shutdownStarted)
            {
                if (exitProcess) Environment.Exit(0);
                return;
            }
            _shutdownStarted = true;
            WriteLog($"=== OVERLAY SHUTDOWN ({reason}) ===");

            try
            {
                _deliveryTimer.Update(false);
                SaveJobState();
                SaveState();
            }
            catch (Exception ex) { WriteLog(ex.ToString()); }

            try { SpeedLimiterService.Instance.ReleaseBrake(); }
            catch (Exception ex) { WriteLog(ex.ToString()); }

            try
            {
                tbTimer?.Stop();
                _idleTimer?.Stop();
                _licenseCheckTimer?.Stop();
                _hudSyncTimer?.Stop();
                _overlayHideTimer?.Stop();
                _cloudSyncDebounceTimer?.Stop();
            }
            catch (Exception ex) { WriteLog(ex.ToString()); }

            try
            {
                if (telemetry != null)
                {
                    telemetry.Data -= Telemetry_Data;
                    telemetry.JobStarted -= Telemetry_JobStarted;
                    telemetry.JobCancelled -= Telemetry_JobCancelled;
                    telemetry.JobDelivered -= Telemetry_JobDelivered;
                    telemetry.Fined -= Telemetry_Fined;
                }
                telemetry?.Dispose();
                telemetry = null;
            }
            catch (Exception ex) { WriteLog(ex.ToString()); }

            try { _tray?.Dispose(); }
            catch (Exception ex) { WriteLog(ex.ToString()); }

            try
            {
                Task.Run(async () => await ClientPresenceService.Instance.ShutdownAsync(reason)).Wait(2500);
            }
            catch (Exception ex) { WriteLog(ex.ToString()); }

            if (exitProcess) Environment.Exit(0);
        }
    }
}
