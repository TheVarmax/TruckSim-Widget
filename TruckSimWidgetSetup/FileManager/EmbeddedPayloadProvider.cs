using System.IO;
using System.IO.Compression;
using System.Reflection;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.FileManager;

public class EmbeddedPayloadProvider : IDisposable
{
    private readonly string? _sourceDirectory;
    private readonly string? _zipFilePath;
    private readonly Stream? _embeddedZipStream;
    private readonly ZipArchive? _zipArchive;
    private readonly PackageManifest _manifest;

    public PackageManifest Manifest => _manifest;

    public EmbeddedPayloadProvider(string? sourceDirectory = null, string? zipFilePath = null)
    {
        _sourceDirectory = sourceDirectory;
        _zipFilePath = zipFilePath;

        // 1. If explicit source directory provided and exists, use it directly (Dev/Test mode)
        if (!string.IsNullOrEmpty(_sourceDirectory) && Directory.Exists(_sourceDirectory))
        {
            InstallerLogger.LogInfo($"Using local source directory payload: {_sourceDirectory}");
            string manifestPath = Path.Combine(_sourceDirectory, "manifest.json");
            if (File.Exists(manifestPath))
            {
                _manifest = PackageManifest.LoadFromFile(manifestPath);
            }
            else
            {
                _manifest = PackageManifest.GenerateFromDirectory(_sourceDirectory, "1.6.6");
            }
            return;
        }

        // 2. If explicit zip file path provided and exists
        if (!string.IsNullOrEmpty(_zipFilePath) && File.Exists(_zipFilePath))
        {
            InstallerLogger.LogInfo($"Using external payload zip: {_zipFilePath}");
            var stream = File.OpenRead(_zipFilePath);
            _zipArchive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            _manifest = LoadManifestFromArchive(_zipArchive);
            return;
        }

        // Normal installs use the payload embedded at build time. External payloads
        // require an explicit developer/test argument and never override this resource.
        var assembly = Assembly.GetExecutingAssembly();
        string[] resourceNames = assembly.GetManifestResourceNames();
        string? payloadResName = resourceNames.FirstOrDefault(r => r.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase));

        if (payloadResName != null)
        {
            InstallerLogger.LogInfo($"Loading embedded payload resource: {payloadResName}");
            _embeddedZipStream = assembly.GetManifestResourceStream(payloadResName);
            if (_embeddedZipStream != null)
            {
                _zipArchive = new ZipArchive(_embeddedZipStream, ZipArchiveMode.Read, leaveOpen: false);
                _manifest = LoadManifestFromArchive(_zipArchive);
                return;
            }
        }

        throw new InvalidOperationException("No embedded payload found. Pass an explicit source or zip path only for developer/test installation.");
    }

    private static PackageManifest LoadManifestFromArchive(ZipArchive archive)
    {
        var entry = archive.GetEntry("manifest.json");
        if (entry == null)
            throw new InvalidOperationException("Payload zip does not contain manifest.json.");

        using var reader = new StreamReader(entry.Open());
        string json = reader.ReadToEnd();
        return PackageManifest.FromJson(json) ?? throw new InvalidOperationException("Failed to parse manifest.json from payload.");
    }

    public void ExtractFile(string relativePath, string installationRoot, string destinationPath)
    {
        string normRel = PackageManifest.NormalizeRelativePath(relativePath);
        string containedDestination = PackageManifest.ResolveContainedPath(installationRoot, relativePath);
        string canonicalDestination = Path.GetFullPath(destinationPath);
        if (!string.Equals(containedDestination, canonicalDestination,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new ArgumentException("Destination path must match the resolved manifest path beneath the installation root.", nameof(destinationPath));
        }

        // Ensure destination directory exists
        string? dir = Path.GetDirectoryName(containedDestination);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (_sourceDirectory != null)
        {
            string sourceFile = PackageManifest.ResolveContainedPath(_sourceDirectory, relativePath);
            if (!File.Exists(sourceFile))
                throw new FileNotFoundException($"Source file not found in payload directory: {sourceFile}");

            File.Copy(sourceFile, containedDestination, overwrite: true);
            return;
        }

        if (_zipArchive != null)
        {
            var entry = _zipArchive.GetEntry(normRel);
            if (entry == null)
            {
                // Try with backslash
                entry = _zipArchive.GetEntry(normRel.Replace('/', '\\'));
            }

            if (entry == null)
                throw new FileNotFoundException($"Entry '{normRel}' not found in payload archive.");

            entry.ExtractToFile(containedDestination, overwrite: true);
            return;
        }

        throw new InvalidOperationException("No active payload source to extract from.");
    }

    public Stream? OpenTelemetryPluginStream()
    {
        // 1. Check embedded resource
        var assembly = Assembly.GetExecutingAssembly();
        string[] resourceNames = assembly.GetManifestResourceNames();
        string? pluginResName = resourceNames.FirstOrDefault(r => r.EndsWith(Constants.PluginFileName, StringComparison.OrdinalIgnoreCase));
        if (pluginResName != null)
        {
            return assembly.GetManifestResourceStream(pluginResName);
        }

        // 2. Check source directory (e.g. source/plugin/scs-telemetry.dll or source/scs-telemetry.dll)
        if (_sourceDirectory != null)
        {
            string p1 = Path.Combine(_sourceDirectory, "plugin", Constants.PluginFileName);
            if (File.Exists(p1)) return File.OpenRead(p1);

            string p2 = Path.Combine(_sourceDirectory, Constants.PluginFileName);
            if (File.Exists(p2)) return File.OpenRead(p2);
        }

        // 3. Check archive if bundled inside
        if (_zipArchive != null)
        {
            var entry = _zipArchive.GetEntry(Constants.PluginFileName) ??
                        _zipArchive.GetEntry($"plugin/{Constants.PluginFileName}");
            if (entry != null)
            {
                return entry.Open();
            }
        }

        return null;
    }

    public void Dispose()
    {
        _zipArchive?.Dispose();
        _embeddedZipStream?.Dispose();
    }
}
