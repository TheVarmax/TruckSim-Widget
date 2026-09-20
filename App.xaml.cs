using System.Configuration;
using System.Data;
using System.Windows;

using System.Threading;

namespace TruckSim_Widget
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "TruckSim_Widget_SingleInstance_Mutex";
            _mutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                MessageBox.Show("TruckSim Widget is already running.", "TruckSim Widget", MessageBoxButton.OK, MessageBoxImage.Information);
                _mutex?.Dispose();
                Environment.Exit(0);
                return;
            }

            SessionEnding += App_SessionEnding;
            base.OnStartup(e);
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
