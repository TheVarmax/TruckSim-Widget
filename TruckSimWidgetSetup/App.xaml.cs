using System.Windows;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;
using TruckSimWidgetSetup.InstallationState;
using TruckSimWidgetSetup.InstallerCore;
using TruckSimWidgetSetup.TransactionEngine;
using TruckSimWidgetSetup.UI.ViewModels;

namespace TruckSimWidgetSetup;

public partial class App : System.Windows.Application
{
    private Mutex? _installerMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Ensure single installer instance
        bool createdNew;
        _installerMutex = new Mutex(true, Constants.InstallerMutexName, out createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "Another instance of TruckSim Widget Installer is already running.",
                "TruckSim Widget Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        // 2. Parse arguments & initialize logging
        var options = InstallOptions.Parse(e.Args);
        string cmdArgs = string.Join(" ", e.Args);
        InstallerLogger.InitSession("1.6.4-beta.1", cmdArgs);

        // 3. Crash recovery check
        CrashRecoveryEngine.CheckAndExecuteRecovery();

        // 4. Detect installation state
        var installInfo = InstallationDetector.Detect();

        // 5. Silent mode execution
        if (options.IsSilent)
        {
            InstallerLogger.LogInfo("Running in SILENT mode.");

            if (options.IsUninstallMode)
            {
                bool success = await InstallerService.ExecuteUninstallAsync(options.RemoveUserDataOnUninstall);
                Shutdown(success ? 0 : 1);
                return;
            }
            else
            {
                bool success = await InstallerService.ExecuteInstallOrUpdateAsync(options, installInfo);
                Shutdown(success ? 0 : 1);
                return;
            }
        }

        // 6. Interactive GUI mode
        var viewModel = new InstallerViewModel(options, installInfo);
        var mainWindow = new MainWindow(viewModel);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _installerMutex?.ReleaseMutex();
        _installerMutex?.Dispose();
        base.OnExit(e);
    }
}
