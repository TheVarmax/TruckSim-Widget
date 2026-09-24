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

        // 1. Parse arguments & initialize logging early
        var options = InstallOptions.Parse(e.Args);
        string cmdArgs = string.Join(" ", e.Args);
        InstallerLogger.InitSession("1.6.4", cmdArgs);

        // 2. Global unhandled exception handlers
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            InstallerLogger.LogErr("Unhandled AppDomain exception", ex);
            if (!options.IsSilent)
            {
                System.Windows.MessageBox.Show(
                    $"A fatal error occurred in TruckSim Widget Setup:\n\n{ex?.Message}\n\nLog: {InstallerLogger.LogFilePath}",
                    "TruckSim Widget Setup - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            InstallerLogger.LogErr("Unhandled Dispatcher exception", args.Exception);
            if (!options.IsSilent)
            {
                System.Windows.MessageBox.Show(
                    $"An unhandled error occurred in TruckSim Widget Setup:\n\n{args.Exception?.Message}\n\nLog: {InstallerLogger.LogFilePath}",
                    "TruckSim Widget Setup - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            args.Handled = true;
            Shutdown(1);
        };

        try
        {
            // 3. Ensure single installer instance
            bool createdNew;
            _installerMutex = new Mutex(true, Constants.InstallerMutexName, out createdNew);
            if (!createdNew)
            {
                InstallerLogger.LogWarn("Another instance of TruckSim Widget Installer is already running. Exiting.");
                if (!options.IsSilent)
                {
                    System.Windows.MessageBox.Show(
                        "Another instance of TruckSim Widget Installer is already running.",
                        "TruckSim Widget Setup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                Shutdown(0);
                return;
            }

            // 4. Crash recovery check
            if (!CrashRecoveryEngine.CheckAndExecuteRecovery())
            {
                throw new InvalidOperationException("A previous installation could not be recovered. Installation has been stopped to preserve its rollback data.");
            }

            // 5. Detect installation state
            var installInfo = InstallationDetector.Detect();

            // 6. Silent mode execution
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

            // 7. Interactive GUI mode
            InstallerLogger.LogInfo("Starting interactive GUI mode.");
            var viewModel = new InstallerViewModel(options, installInfo);
            var mainWindow = new MainWindow(viewModel);
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            InstallerLogger.LogErr("Fatal exception during installer startup", ex);
            if (!options.IsSilent)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to start TruckSim Widget Setup:\n\n{ex.Message}\n\nLog: {InstallerLogger.LogFilePath}",
                    "TruckSim Widget Setup - Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _installerMutex?.ReleaseMutex();
        }
        catch { }
        _installerMutex?.Dispose();
        base.OnExit(e);
    }
}
