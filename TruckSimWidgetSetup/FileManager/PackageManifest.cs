using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TruckSimWidgetSetup.Common;

namespace TruckSimWidgetSetup.FileManager;

public class ManifestFileEntry
{
    [JsonPropertyName("path")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;
}

public class PackageManifest
{
    private static bool IsWindowsDeviceName(string segment)
    {
        string deviceName = segment.Split('.', 2)[0].TrimEnd(' ', '.');
        if (deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase))
            return true;

        if (deviceName.Length != 4 ||
            !(deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
              deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)))
            return false;

        return deviceName[3] is >= '1' and <= '9' or '\u00b9' or '\u00b2' or '\u00b3';
    }

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

    [JsonPropertyName("files")]
    public List<ManifestFileEntry> Files { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static PackageManifest? FromJson(string json) =>
        JsonSerializer.Deserialize<PackageManifest>(json, JsonOptions);

    public static PackageManifest LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Manifest file not found", path);

        string json = File.ReadAllText(path);
        return FromJson(json) ?? throw new InvalidOperationException("Failed to deserialize manifest.");
    }

    public void SaveToFile(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, ToJson());
    }

    public bool TryGetEntry(string relativePath, out ManifestFileEntry? entry)
    {
        string norm = NormalizeRelativePath(relativePath);
        entry = Files.FirstOrDefault(f => string.Equals(NormalizeRelativePath(f.RelativePath), norm, StringComparison.OrdinalIgnoreCase));
        return entry != null;
    }

    public static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    /// <summary>
    /// Resolves a manifest path beneath a trusted root. Manifest paths are untrusted,
    /// so reject rooted/device paths and parent segments before canonicalizing and
    /// checking the final path boundary.
    /// </summary>
    public static string ResolveContainedPath(string rootDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("A root directory is required.", nameof(rootDirectory));
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("A relative manifest path is required.", nameof(relativePath));

        string normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.Contains(':') ||
            Path.IsPathRooted(normalized))
        {
            throw new ArgumentException("Manifest paths must be relative paths.", nameof(relativePath));
        }

        string[] segments = normalized.Split('/');
        if (segments.Any(segment =>
                segment.Length == 0 ||
                segment == ".." ||
                (OperatingSystem.IsWindows() && segment != "." &&
                    (segment.EndsWith(' ') || segment.EndsWith('.') || IsWindowsDeviceName(segment)))))
            throw new ArgumentException("Manifest paths cannot contain empty or parent path segments.", nameof(relativePath));

        string canonicalRoot = Path.GetFullPath(rootDirectory);
        string platformRelativePath = Path.Combine(segments.Where(segment => segment != ".").ToArray());
        if (string.IsNullOrEmpty(platformRelativePath))
            throw new ArgumentException("Manifest paths must name a file beneath the root directory.", nameof(relativePath));

        string canonicalPath = Path.GetFullPath(platformRelativePath, canonicalRoot);
        string rootPrefix = Path.EndsInDirectorySeparator(canonicalRoot)
            ? canonicalRoot
            : canonicalRoot + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!canonicalPath.StartsWith(rootPrefix, comparison))
            throw new ArgumentException("Manifest path resolves outside the allowed root directory.", nameof(relativePath));

        EnsureNoReparsePointWithinRoot(canonicalRoot, canonicalPath);

        return canonicalPath;
    }

    private static void EnsureNoReparsePointWithinRoot(string canonicalRoot, string canonicalPath)
    {
        if (!OperatingSystem.IsWindows()) return;
        string? current = canonicalRoot;
        string relative = Path.GetRelativePath(canonicalRoot, canonicalPath);
        string[] components = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (string component in new[] { "" }.Concat(components))
        {
            if (!string.IsNullOrEmpty(component)) current = Path.Combine(current!, component);
            if (!File.Exists(current) && !Directory.Exists(current)) continue;
            try
            {
                if ((File.GetAttributes(current!) & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("Manifest paths cannot pass through reparse points.", nameof(canonicalPath));
            }
            catch (ArgumentException) { throw; }
            catch (IOException ex)
            {
                throw new ArgumentException("Unable to inspect an existing manifest path component safely.", nameof(canonicalPath), ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new ArgumentException("Unable to inspect an existing manifest path component safely.", nameof(canonicalPath), ex);
            }
        }
    }

    public static string ComputeFileSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Generates manifest from application publish directory.
    /// Excludes plugin/ and scs-telemetry.dll per architecture rule (plugin is managed separately).
    /// </summary>
    public static PackageManifest GenerateFromDirectory(string sourceDir, string version)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        var manifest = new PackageManifest
        {
            Version = version,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        var allFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

        foreach (var file in allFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            string rel = Path.GetRelativePath(sourceDir, file);
            string normRel = NormalizeRelativePath(rel);

            // STRICT RULE: plugin/scs-telemetry.dll is NOT part of application manifest
            if (normRel.StartsWith("plugin/", StringComparison.OrdinalIgnoreCase) ||
                normRel.Equals("plugin", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(normRel).Equals(Constants.PluginFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Exclude installer/uninstaller artifacts that might be left in publish dir
            if (Path.GetFileName(normRel).StartsWith("TruckSimWidgetSetup", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(normRel).StartsWith("unins000", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fi = new FileInfo(file);
            manifest.Files.Add(new ManifestFileEntry
            {
                RelativePath = normRel,
                Size = fi.Length,
                Sha256 = ComputeFileSha256(file)
            });
        }

        return manifest;
    }
}
