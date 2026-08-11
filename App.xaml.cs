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
                Environment.Exit(0);
                return;
            }

            base.OnStartup(e);
        }
    }
}
