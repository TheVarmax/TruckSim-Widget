using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.InstallationState;

namespace TruckSimWidgetSetup.FileManager;

public static class OwnershipManager
{
    /// <summary>
    /// Evaluates whether a file located inside the application directory is owned by the installer.
    /// STRICT RULE: Unknown / user files return FALSE and must never be overwritten or deleted.
    /// </summary>
    public static bool IsFileOwned(
        string relativePath,
        PackageManifest currentManifest,
        PackageManifest? previousManifest)
    {
        string normRel = PackageManifest.NormalizeRelativePath(relativePath);

        // 1. If present in current release manifest -> Owned
        if (currentManifest.TryGetEntry(normRel, out _))
        {
            return true;
        }

        // 2. If present in recorded previous manifest -> Owned
        if (previousManifest != null && previousManifest.TryGetEntry(normRel, out _))
        {
            return true;
        }

        // Any other file is Unknown / User created
        return false;
    }

    /// <summary>
    /// Evaluates whether an existing TruckSimWidgetSetup.exe in the application directory
    /// is owned by an existing installation.
    /// STRICT RULE: Only returns true if install-state.json explicitly records it in InstalledFiles.
    /// Being a valid installation alone is not enough to treat an unrecorded executable as owned.
    /// </summary>
    public static bool IsInstallerExeOwned(string fullPath, InstallationInfo installInfo)
    {
        if (installInfo.ExistingState != null &&
            installInfo.ExistingState.InstalledFiles.Any(f => string.Equals(f.RelativePath, Constants.InstallerExeName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}
