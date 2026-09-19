namespace TruckSimWidgetSetup.Common;

public static class Constants
{
    public const string AppId = "{8F4E6E2C-7F11-4F7D-BD7D-TRUCKSIMWIDGET}";
    public const string AppName = "TruckSim Widget";
    public const string AppExeName = "TruckSim Widget.exe";
    public const string InstallerExeName = "TruckSimWidgetSetup.exe";
    public const string Publisher = "TheVarmax";
    public const string PublisherUrl = "https://trucksim.uk";
    public const string AppSupportUrl = "https://trucksim.uk";
    public const string AppUpdatesUrl = "https://github.com/TheVarmax/TruckSim-Widget/releases";

    // Registry
    public const string UninstallRegSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F4E6E2C-7F11-4F7D-BD7D-TRUCKSIMWIDGET}_is1";
    public const string WidgetSettingsRegSubKey = @"Software\TruckSim Widget";

    // Mutexes
    public const string AppMutexName = "TruckSim_Widget_SingleInstance_Mutex";
    public const string InstallerMutexName = "TruckSim_Widget_Installer_Mutex";

    // Games
    public const string GameIdEts2 = "ETS2";
    public const string GameIdAts = "ATS";
    public const int SteamAppIdEts2 = 227300;
    public const int SteamAppIdAts = 270880;
    public const string GameExeEts2 = "eurotrucks2.exe";
    public const string GameExeAts = "amtrucks.exe";
    public const string GameDirNameEts2 = "Euro Truck Simulator 2";
    public const string GameDirNameAts = "American Truck Simulator";
    public const string GameRelBinDir = @"bin\win_x64";
    public const string GameRelPluginDir = @"bin\win_x64\plugins";
    public const string PluginFileName = "scs-telemetry.dll";
    public const string PluginBackupExt = ".trucksim_backup";

    // Paths
    public static string GetDefaultAppDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);

    public static string GetUserDataDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TruckSimWidget");

    public static string GetInstallerDir() =>
        Path.Combine(GetUserDataDir(), "installer");

    public static string GetInstallStateFilePath() =>
        Path.Combine(GetInstallerDir(), "install-state.json");

    public static string GetInstalledManifestFilePath() =>
        Path.Combine(GetInstallerDir(), "installed-manifest.json");

    public static string GetTransactionJournalFilePath() =>
        Path.Combine(GetInstallerDir(), "transaction_journal.json");

    public static string GetTransactionStagingDir() =>
        Path.Combine(GetInstallerDir(), "staging");

    public static string GetInstallerLogFilePath() =>
        Path.Combine(GetInstallerDir(), "installer_log.txt");

    public static string GetStartMenuProgramsDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName);

    public static string GetDesktopDir() =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
}
