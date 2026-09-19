using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.GameDiscovery;

namespace TruckSimWidgetSetup.InstallerCore;

public class InstallOptions
{
    public bool IsUpdateMode { get; set; }
    public bool IsReinstallMode { get; set; }
    public bool IsUninstallMode { get; set; }
    public bool IsSilent { get; set; }
    public bool CreateDesktopShortcut { get; set; }
    public bool LaunchAppAfter { get; set; } = true;
    public string? CustomInstallDir { get; set; }
    public string? CustomSourceDir { get; set; }
    public string? CustomZipPath { get; set; }
    public bool RemoveUserDataOnUninstall { get; set; }

    public GameConfig Ets2Config { get; set; } = GameConfig.CreateEts2();
    public GameConfig AtsConfig { get; set; } = GameConfig.CreateAts();

    public static InstallOptions Parse(string[] args)
    {
        var options = new InstallOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].Trim();

            if (string.Equals(arg, "--update", StringComparison.OrdinalIgnoreCase))
                options.IsUpdateMode = true;
            else if (string.Equals(arg, "--reinstall", StringComparison.OrdinalIgnoreCase))
                options.IsReinstallMode = true;
            else if (string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase))
                options.IsUninstallMode = true;
            else if (string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "/silent", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "/verysilent", StringComparison.OrdinalIgnoreCase))
                options.IsSilent = true;
            else if (string.Equals(arg, "--desktop-shortcut", StringComparison.OrdinalIgnoreCase))
                options.CreateDesktopShortcut = true;
            else if (string.Equals(arg, "--no-launch", StringComparison.OrdinalIgnoreCase))
                options.LaunchAppAfter = false;
            else if (string.Equals(arg, "--delete-user-data", StringComparison.OrdinalIgnoreCase))
                options.RemoveUserDataOnUninstall = true;
            else if (string.Equals(arg, "--dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                options.CustomInstallDir = args[++i];
            else if (string.Equals(arg, "--source", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                options.CustomSourceDir = args[++i];
            else if (string.Equals(arg, "--zip", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                options.CustomZipPath = args[++i];
        }

        return options;
    }
}
