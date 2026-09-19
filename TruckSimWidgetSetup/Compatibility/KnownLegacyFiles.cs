using System.Diagnostics;
using System.Text.RegularExpressions;
using TruckSimWidgetSetup.Common;

namespace TruckSimWidgetSetup.Compatibility;

public static class KnownLegacyFiles
{
    private static readonly Regex InnoServiceRegex = new(
        @"^unins\d{3,}\.(exe|dat|msg)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Identifies files generated strictly by Inno Setup for uninstallation.
    /// These are always installer-owned service artifacts.
    /// </summary>
    public static bool IsLegacyInnoServiceFile(string relativePath)
    {
        string fileName = Path.GetFileName(relativePath);
        return InnoServiceRegex.IsMatch(fileName);
    }

    /// <summary>
    /// Conservative check for legacy application binaries.
    /// Avoids loose filename matching for generic DLLs or user files.
    /// Checks specific PE metadata for TruckSim Widget products.
    /// </summary>
    public static bool IsLegacyUnambiguousAppBinary(string fullPath)
    {
        if (!File.Exists(fullPath)) return false;

        string fileName = Path.GetFileName(fullPath);

        // Check exact core executable names
        if (fileName.Equals(Constants.AppExeName, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("ElevatedHelper.exe", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("updater.exe", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(fullPath);
                // Check publisher or product name
                if (string.Equals(vi.CompanyName, Constants.Publisher, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(vi.ProductName, Constants.AppName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(vi.InternalName, "ElevatedHelper", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(vi.InternalName, "updater", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(vi.InternalName, "TruckSim Widget", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // If version info cannot be read, still check filename match for known exes
                return true;
            }
        }

        return false;
    }
}
