using TruckSimWidgetSetup.Compatibility;

namespace TruckSimWidgetSetup.FileManager;

public static class OwnershipManager
{
    /// <summary>
    /// Evaluates whether a file located inside the application directory is owned by the installer.
    /// STRICT RULE: Unknown / user files return FALSE and must never be overwritten or deleted.
    /// </summary>
    public static bool IsFileOwned(
        string relativePath,
        string fullPath,
        PackageManifest currentManifest,
        PackageManifest? previousManifest,
        bool isLegacyTakeover)
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

        // 3. In legacy Inno takeover mode
        if (isLegacyTakeover)
        {
            // Explicit Inno service artifacts (unins000.exe, unins000.dat, unins000.msg)
            if (KnownLegacyFiles.IsLegacyInnoServiceFile(normRel))
            {
                return true;
            }

            // Unambiguous core binaries belonging to TruckSim Widget
            if (KnownLegacyFiles.IsLegacyUnambiguousAppBinary(fullPath))
            {
                return true;
            }
        }

        // Any other file is Unknown / User created
        return false;
    }
}
