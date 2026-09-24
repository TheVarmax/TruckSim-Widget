namespace TruckSimWidgetSetup.InstallationState;

public enum InstallationStatus
{
    NotInstalled,
    StaleDirectory,
    Installed
}

public class InstallationInfo
{
    public InstallationStatus Status { get; set; } = InstallationStatus.NotInstalled;
    public string InstallPath { get; set; } = string.Empty;
    public string InstalledVersion { get; set; } = string.Empty;
    public InstallStateModel? ExistingState { get; set; }

    public bool IsInstalled => Status == InstallationStatus.Installed;
}
