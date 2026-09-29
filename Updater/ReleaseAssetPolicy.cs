namespace TruckSimUpdater;

internal static class ReleaseAssetPolicy
{
    internal static bool IsTrustedDownload(string? downloadUrl, string? assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName) || string.IsNullOrWhiteSpace(downloadUrl)
            || !Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttps
            && uri.IsDefaultPort
            && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && uri.UserInfo.Length == 0
            && uri.Query.Length == 0
            && uri.Fragment.Length == 0
            && !uri.AbsolutePath.Contains('\\')
            && uri.AbsolutePath.StartsWith("/TheVarmax/TruckSim-Widget/releases/download/", StringComparison.OrdinalIgnoreCase)
            && Uri.UnescapeDataString(uri.Segments[^1]).Equals(assetName, StringComparison.OrdinalIgnoreCase);
    }
}
