using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.PluginManager;

namespace TruckSimWidgetSetup.GameDiscovery;

public class GameConfig
{
    public string GameId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ExeName { get; set; } = string.Empty;
    public string DefaultFolderName { get; set; } = string.Empty;
    public int SteamAppId { get; set; }
    public bool UserSelected { get; set; }
    public string DetectedPath { get; set; } = string.Empty;
    public string SelectedPath { get; set; } = string.Empty;
    public string TargetPluginFile => string.IsNullOrEmpty(SelectedPath)
        ? string.Empty
        : Path.Combine(SelectedPath, Constants.GameRelPluginDir, Constants.PluginFileName);

    public bool ExistingFileFound { get; set; }
    public string ExistingFileHash { get; set; } = string.Empty;
    public string OwnershipStatus { get; set; } = PluginOwnershipStatus.None;
    public PluginConflictAction ConflictAction { get; set; } = PluginConflictAction.None;
    public string BackupPath { get; set; } = string.Empty;
    public string BackupHash { get; set; } = string.Empty;
    public bool NeedsElevation { get; set; }
    public bool IsRunning { get; set; }

    public static GameConfig CreateEts2() => new()
    {
        GameId = Constants.GameIdEts2,
        DisplayName = "Euro Truck Simulator 2",
        ExeName = Constants.GameExeEts2,
        DefaultFolderName = Constants.GameDirNameEts2,
        SteamAppId = Constants.SteamAppIdEts2
    };

    public static GameConfig CreateAts() => new()
    {
        GameId = Constants.GameIdAts,
        DisplayName = "American Truck Simulator",
        ExeName = Constants.GameExeAts,
        DefaultFolderName = Constants.GameDirNameAts,
        SteamAppId = Constants.SteamAppIdAts
    };
}
