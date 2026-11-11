using System.Configuration;
using System.Data;
using System.Windows;

using System.Threading;
using ETSOverlay;

namespace TruckSim_Widget
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // No "Global\" prefix: both names live in the user's own session.
        private const string MutexName = "TruckSim_Widget_SingleInstance_Mutex";
        private const string RestoreEventName = "TruckSim_Widget_RestoreInstance_Event";

        private static Mutex? _mutex;
        private static EventWaitHandle? _restoreEvent;
        private static Thread? _restoreListener;
        private static volatile bool _exiting;

        protected override void OnStartup(StartupEventArgs e)
        {
            // The launch reason is known before any WPF window is created or shown.
            LaunchContext.Initialize(e.Args);

            _mutex = new Mutex(true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is already running. A start from the shortcut brings its widget
                // back; a repeated autostart ends quietly.
                if (LaunchContext.Current != LaunchReason.Autostart)
                {
                    SignalRunningInstance();
                }
                _mutex?.Dispose();
                _mutex = null;
                Environment.Exit(0);
                return;
            }

            StartRestoreListener();
            SessionEnding += App_SessionEnding;
            base.OnStartup(e);
        }

        private static void SignalRunningInstance()
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting(RestoreEventName, out var handle))
                {
                    using (handle) handle.Set();
                }
            }
            catch { }
        }

        private void StartRestoreListener()
        {
            try
            {
                _restoreEvent = new EventWaitHandle(false, EventResetMode.AutoReset, RestoreEventName);
                _restoreListener = new Thread(() =>
                {
                    while (!_exiting)
                    {
                        try
                        {
                            if (!_restoreEvent.WaitOne()) continue;
                            if (_exiting) break;
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (MainWindow is ETSOverlay.MainWindow mw)
                                {
                                    mw.RestoreFromTray("started again");
                                }
                            }));
                        }
                        catch
                        {
                            break;
                        }
                    }
                })
                {
                    IsBackground = true,
                    Name = "RestoreInstanceListener"
                };
                _restoreListener.Start();
            }
            catch { }
        }

        private void App_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            try
            {
                if (MainWindow is ETSOverlay.MainWindow mw)
                {
                    mw.HandleSessionEnding();
                }
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _exiting = true;
            try
            {
                _restoreEvent?.Set();
                _restoreEvent?.Dispose();
            }
            catch { }
            _restoreEvent = null;

            try
            {
                _mutex?.ReleaseMutex();
            }
            catch { }
            try
            {
                _mutex?.Dispose();
            }
            catch { }
            _mutex = null;

            base.OnExit(e);
        }
    }
}
