namespace TruckSimWidgetSetup.PluginManager;

public static class PluginOwnershipStatus
{
    public const string None = "None";
    public const string Owned = "Owned";
    public const string LegacyOwnedUnverified = "LegacyOwnedUnverified";
    public const string ThirdPartyReplaced = "ThirdPartyReplaced";
    public const string Skipped = "Skipped";
    public const string ModifiedByUser = "ModifiedByUser";
}

public enum PluginConflictAction
{
    None = 0,
    BackupReplace = 1,
    KeepExisting = 2,
    Overwrite = 3
}
