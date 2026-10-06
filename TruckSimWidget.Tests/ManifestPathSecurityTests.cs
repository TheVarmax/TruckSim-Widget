using System.IO;
using System.IO.Compression;
using System.Text;
using TruckSimWidgetSetup.FileManager;
using Xunit;

namespace TruckSimWidget.Tests;
[Collection("Installer integrity")]
public class ManifestPathSecurityTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), "tsw_manifest_path_tests_" + Guid.NewGuid().ToString("N"));

    public ManifestPathSecurityTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [Theory]
    [InlineData(@"..\evil.exe")]
    [InlineData(@"sub\..\..\evil.exe")]
    [InlineData("../evil.exe")]
    [InlineData("/evil.exe")]
    [InlineData(@"C:\evil.exe")]
    [InlineData(@"C:evil.exe")]
    [InlineData(@"\\server\share\evil.exe")]
    [InlineData(@"\\?\C:\evil.exe")]
    [InlineData(@"\\.\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\evil.exe")]
    [InlineData(@"sub/../..\evil.exe")]
    [InlineData("CON")]
    [InlineData("CON.txt")]
    [InlineData("CONIN$")]
    [InlineData("CONOUT$.txt")]
    [InlineData("NUL.dll")]
    [InlineData("sub\\NUL.dll")]
    [InlineData("COM1.exe")]
    [InlineData("COM1.log")]
    [InlineData("LPT9")]
    [InlineData("file.")]
    [InlineData("file ")]
    public void ResolveContainedPath_RejectsTraversalAndRootedWindowsPaths(string path)
    {
        string installRoot = Path.Combine(_testRoot, "install");
        Directory.CreateDirectory(installRoot);

        Assert.Throws<ArgumentException>(() => PackageManifest.ResolveContainedPath(installRoot, path));
    }

    [Fact]
    public void ResolveContainedPath_AllowsNestedFilesAndCanonicalizesInsideRoot()
    {
        string installRoot = Path.Combine(_testRoot, "install");
        Directory.CreateDirectory(installRoot);

        string nested = PackageManifest.ResolveContainedPath(installRoot, @"subdir\file.dll");
        string canonical = PackageManifest.ResolveContainedPath(installRoot, "subdir/./file.dll");

        Assert.Equal(Path.Combine(Path.GetFullPath(installRoot), "subdir", "file.dll"), nested);
        Assert.Equal(nested, canonical);
        Assert.StartsWith(Path.GetFullPath(installRoot) + Path.DirectorySeparatorChar, canonical, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FileSynchronizer_RejectsUnsafeManifestBeforeProducingAPlan()
    {
        string installRoot = Path.Combine(_testRoot, "install");
        Directory.CreateDirectory(installRoot);
        var manifest = new PackageManifest
        {
            Files = new() { new ManifestFileEntry { RelativePath = @"..\outside.dll" } },
        };

        Assert.Throws<ArgumentException>(() => FileSynchronizer.PlanSynchronization(installRoot, manifest, null));
    }

    [Fact]
    public void ZipPayloadExtraction_RejectsTraversalEntryWithoutWritingOutsideRoot()
    {
        string archivePath = Path.Combine(_testRoot, "payload.zip");
        string installRoot = Path.Combine(_testRoot, "install");
        string outsidePath = Path.Combine(_testRoot, "outside.dll");
        Directory.CreateDirectory(installRoot);

        using (var stream = File.Create(archivePath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8))
                writer.Write("{\"version\":\"1.0\",\"files\":[{\"path\":\"../outside.dll\",\"size\":6,\"sha256\":\"\"}]}");

            using var traversalEntry = archive.CreateEntry("../outside.dll").Open();
            using var payloadWriter = new StreamWriter(traversalEntry, Encoding.UTF8);
            payloadWriter.Write("payload");
        }

        using var payload = new EmbeddedPayloadProvider(zipFilePath: archivePath);
        Assert.Throws<ArgumentException>(() => payload.ExtractFile("../outside.dll", installRoot, outsidePath));
        Assert.False(File.Exists(outsidePath));
    }
}
