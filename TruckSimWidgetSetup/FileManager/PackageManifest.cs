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
