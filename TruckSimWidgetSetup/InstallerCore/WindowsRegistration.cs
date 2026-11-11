using Microsoft.Win32;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.InstallerCore;

/// <summary>Registry access of the "Apps &amp; features" entry (replaceable in tests).</summary>
public interface IUninstallRegistry
{
    /// <summary>Writes the values of the entry. False when the key cannot be created or opened.</summary>
    bool Write(IReadOnlyList<KeyValuePair<string, object>> values);
    /// <summary>Removes the entry. Nothing happens when it does not exist.</summary>
    void Delete();
}

internal sealed class CurrentUserUninstallRegistry : IUninstallRegistry
{
    public bool Write(IReadOnlyList<KeyValuePair<string, object>> values)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Constants.UninstallRegSubKey);
        if (key == null) return false;

        foreach (var (name, value) in values)
        {
            if (value is int number) key.SetValue(name, number, RegistryValueKind.DWord);
            else key.SetValue(name, value);
        }
        return true;
    }

    public void Delete()
    {
        using var uninstallRoot = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", writable: true);
        if (uninstallRoot == null) return;

        string subKeyName = Path.GetFileName(Constants.UninstallRegSubKey);
        uninstallRoot.DeleteSubKeyTree(subKeyName, throwOnMissingSubKey: false);
    }
}

public static class WindowsRegistration
{
    /// <summary>
    /// Registry used by the installer. Tests replace it, so they never write or remove the
    /// registration of a widget that is really installed on the machine.
    /// </summary>
    internal static IUninstallRegistry Registry { get; set; } = new CurrentUserUninstallRegistry();

    public static bool Register(string appDir, string version)
    {
        try
        {
            string exePath = Path.Combine(appDir, Constants.AppExeName);
            string installerPath = Path.Combine(appDir, Constants.InstallerExeName);

            var values = new List<KeyValuePair<string, object>>
            {
                new("DisplayName", Constants.AppName),
                new("DisplayVersion", version),
                new("Publisher", Constants.Publisher),
                new("InstallLocation", appDir),
                new("DisplayIcon", exePath),
                new("UninstallString", $"\"{installerPath}\" --uninstall"),
                new("QuietUninstallString", $"\"{installerPath}\" --uninstall --silent"),
                new("InstallDate", DateTime.Now.ToString("yyyyMMdd")),
                new("NoModify", 1),
                new("NoRepair", 1),
                new("URLInfoAbout", Constants.PublisherUrl),
                new("HelpLink", Constants.AppSupportUrl),
                new("URLUpdateInfo", Constants.AppUpdatesUrl)
            };

            if (Registry.Write(values))
            {
                InstallerLogger.LogInfo($"Registered application in Windows Uninstall registry under: {Constants.UninstallRegSubKey}");
                return true;
            }

            InstallerLogger.LogErr($"Failed to create or open registry key: {Constants.UninstallRegSubKey}");
            return false;
        }
        catch (Exception ex)
        {
            InstallerLogger.LogErr($"Failed to register application in Windows registry: {ex.Message}");
            return false;
        }
    }

    public static void Unregister()
    {
        try
        {
            Registry.Delete();
            InstallerLogger.LogInfo($"Removed Windows Uninstall registration: {Constants.UninstallRegSubKey}");
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Failed to remove Windows Uninstall registry key: {ex.Message}");
        }
    }
}
