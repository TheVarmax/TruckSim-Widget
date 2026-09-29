using System.Text.RegularExpressions;

namespace TruckSimWidgetSetup.InstallerCore;

internal static partial class PackageVersion
{
    [GeneratedRegex(@"^v?(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z.-]+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();

    internal static bool IsDowngrade(string candidate, string installed)
    {
        var next = Parse(candidate) ?? throw new InvalidOperationException("Payload version is invalid.");
        var current = Parse(installed);
        return current is not null && Compare(next, current.Value) < 0;
    }

    private static (Version Numeric, string? Prerelease)? Parse(string text)
    {
        var match = VersionPattern().Match(text.Trim());
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var major)
            || !int.TryParse(match.Groups[2].Value, out var minor)
            || !int.TryParse(match.Groups[3].Value, out var patch)) return null;
        return (new Version(major, minor, patch), match.Groups[4].Success ? match.Groups[4].Value : null);
    }

    private static int Compare((Version Numeric, string? Prerelease) a, (Version Numeric, string? Prerelease) b)
    {
        int numeric = a.Numeric.CompareTo(b.Numeric);
        if (numeric != 0) return numeric;
        if (a.Prerelease is null) return b.Prerelease is null ? 0 : 1;
        if (b.Prerelease is null) return -1;
        var first = a.Prerelease.Split('.');
        var second = b.Prerelease.Split('.');
        for (int i = 0; i < Math.Min(first.Length, second.Length); i++)
        {
            bool firstNumber = int.TryParse(first[i], out int x);
            bool secondNumber = int.TryParse(second[i], out int y);
            int order = firstNumber && secondNumber ? x.CompareTo(y)
                : firstNumber ? -1 : secondNumber ? 1
                : string.Compare(first[i], second[i], StringComparison.OrdinalIgnoreCase);
            if (order != 0) return order;
        }
        return first.Length.CompareTo(second.Length);
    }
}
