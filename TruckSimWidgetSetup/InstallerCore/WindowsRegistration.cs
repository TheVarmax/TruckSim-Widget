using Microsoft.Win32;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.InstallerCore;

public static class WindowsRegistration
{
    public static void Register(string appDir, string version)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(Constants.UninstallRegSubKey);
            if (key != null)
            {
                string exePath = Path.Combine(appDir, Constants.AppExeName);
                string installerPath = Path.Combine(appDir, Constants.InstallerExeName);

                key.SetValue("DisplayName", Constants.AppName);
                key.SetValue("DisplayVersion", version);
                key.SetValue("Publisher", Constants.Publisher);
                key.SetValue("InstallLocation", appDir);
                key.SetValue("DisplayIcon", exePath);
                key.SetValue("UninstallString", $"\"{installerPath}\" --uninstall");
                key.SetValue("QuietUninstallString", $"\"{installerPath}\" --uninstall --silent");
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("URLInfoAbout", Constants.PublisherUrl);
                key.SetValue("HelpLink", Constants.AppSupportUrl);
                key.SetValue("URLUpdateInfo", Constants.AppUpdatesUrl);

                InstallerLogger.LogInfo($"Registered application in Windows Uninstall registry under: {Constants.UninstallRegSubKey}");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.LogErr($"Failed to register application in Windows registry: {ex.Message}");
        }
    }

    public static void Unregister()
    {
        try
        {
            using var uninstallRoot = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", writable: true);
            if (uninstallRoot != null)
            {
                string subKeyName = Path.GetFileName(Constants.UninstallRegSubKey);
                uninstallRoot.DeleteSubKeyTree(subKeyName, throwOnMissingSubKey: false);
                InstallerLogger.LogInfo($"Removed Windows Uninstall registration: {Constants.UninstallRegSubKey}");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Failed to remove Windows Uninstall registry key: {ex.Message}");
        }
    }
}
