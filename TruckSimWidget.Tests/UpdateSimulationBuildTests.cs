#if UPDATE_SIMULATION_VERIFICATION
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace TruckSimWidget.Tests;

[CollectionDefinition("Update simulation", DisableParallelization = true)]
public class UpdateSimulationCollection { }

[Collection("Update simulation")]
public class UpdateSimulationBuildTests
{
    private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Type Widget = typeof(ETSOverlay.MainWindow);
    private static readonly Type Updater = Assembly.Load("updater").GetType("TruckSimUpdater.Program", true)!;
    private static readonly Type FormType = Updater.GetNestedType("UpdateForm", BindingFlags.NonPublic)!;
    private static readonly Dictionary<short, OpCode> Opcodes = typeof(OpCodes).GetFields()
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(op => op.Value);

    [Fact]
    public void CompiledSimulationSurfaceMatchesConfiguration()
    {
        string[] methods = { "CheckUpdateSimulationArgs", "FindCandidateInstaller", "ComputeFileSha256" };
        string[] fields = { "_isUpdateSimulationActive", "_simulationInstallerPath", "_simulationVersion" };
#if DEBUG
        foreach (string name in methods) Assert.NotNull(Widget.GetMethod(name, HiddenStatic));
        foreach (string name in fields) Assert.NotNull(Widget.GetField(name, HiddenStatic));
        Assert.NotNull(Updater.GetMethod("TryGetLocalSimulationSource", HiddenStatic));
#else
        foreach (string name in methods) Assert.Null(Widget.GetMethod(name, HiddenStatic));
        foreach (string name in fields) Assert.Null(Widget.GetField(name, HiddenStatic));
        Assert.Null(Updater.GetMethod("TryGetLocalSimulationSource", HiddenStatic));
        var download = Calls(AsyncBody(FormType, "DownloadWithProgressAsync"));
        Assert.DoesNotContain(download, s => s.Contains("TryGetLocalSimulationSource"));
        Assert.DoesNotContain(download, s => s == "System.IO.File::Exists" || s == "System.IO.Path::IsPathRooted");
        Assert.DoesNotContain(Strings(AsyncBody(FormType, "DownloadWithProgressAsync")),
            s => s.Contains("Local update file") || s.Contains("file://"));
#endif
    }

#if DEBUG
    [Theory]
    [InlineData("--simulate-update")]
    [InlineData("--test-update")]
    [InlineData("-simulate-update")]
    [InlineData("-test-update")]
    [InlineData("--simulate-update=")]
    [InlineData("--test-update=")]
    public void DebugParserPreservesDeveloperArguments(string flag)
    {
        Widget.GetField("_isUpdateSimulationActive", HiddenStatic)!.SetValue(null, false);
        Widget.GetField("_simulationInstallerPath", HiddenStatic)!.SetValue(null, null);
        Widget.GetField("_simulationVersion", HiddenStatic)!.SetValue(null, null);
        const string path = @"C:\Developer\TruckSimWidgetSetup.exe";
        string[] args = flag.EndsWith('=') ? new[] { flag + path, "--simulate-version=9.0.0" }
            : new[] { flag, path, "--test-version", "9.0.0" };
        Assert.True((bool)Widget.GetMethod("CheckUpdateSimulationArgs", HiddenStatic)!.Invoke(null, new object[] { args })!);
        Assert.Equal(path, Widget.GetField("_simulationInstallerPath", HiddenStatic)!.GetValue(null));
        Assert.Equal("9.0.0", Widget.GetField("_simulationVersion", HiddenStatic)!.GetValue(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DebugLocalPackageCanBeDownloaded(bool fileUri)
    {
        WithTempDirectory(dir =>
        {
            string source = Path.Combine(dir, "TruckSimWidgetSetup.exe");
            byte[] payload = Encoding.UTF8.GetBytes("developer package");
            File.WriteAllBytes(source, payload);
            string input = fileUri ? new Uri(source).AbsoluteUri : source;
            object[] arguments = { input, "" };
            Assert.True((bool)Updater.GetMethod("TryGetLocalSimulationSource", HiddenStatic)!.Invoke(null, arguments)!);
            Assert.Equal(source, arguments[1]);
            string destination = Path.Combine(dir, "download.exe");
            Download(input, destination);
            Assert.Equal(payload, File.ReadAllBytes(destination));
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant(),
                Widget.GetMethod("ComputeFileSha256", HiddenStatic)!.Invoke(null, new object[] { destination }));
        });
    }

    [Theory]
    [InlineData("https://github.com/TheVarmax/TruckSim-Widget/releases/download/v1/TruckSimWidgetSetup.exe")]
    [InlineData("http://example.com/package.exe")]
    [InlineData("--simulate-update")]
    [InlineData("--local")]
    public void DebugClassifierDoesNotTreatNetworkOrFlagsAsLocal(string input)
    {
        Assert.False((bool)Updater.GetMethod("TryGetLocalSimulationSource", HiddenStatic)!
            .Invoke(null, new object[] { input, "" })!);
    }
#else
    [Theory]
    [InlineData(@"C:\Developer\TruckSimWidgetSetup.exe")]
    [InlineData("file:///C:/Developer/TruckSimWidgetSetup.exe")]
    [InlineData("--simulate-update")]
    [InlineData("--test-update")]
    [InlineData("--local")]
    public void ReleaseDownloaderCannotCopyLocalOrSimulationInputs(string input)
    {
        WithTempDirectory(dir =>
        {
            string source = Path.Combine(dir, "source.exe");
            File.WriteAllText(source, "local source must not be consumed");
            foreach (string url in new[] { input, source, new Uri(source).AbsoluteUri })
            {
                string destination = Path.Combine(dir, "download.exe");
                Assert.ThrowsAny<Exception>(() => Download(url, destination));
                Assert.False(File.Exists(destination));
            }
        });
    }
#endif

    [Theory]
    [InlineData("https://github.com/TheVarmax/TruckSim-Widget/releases/download/v1/TruckSimWidgetSetup.exe", true)]
    [InlineData("http://github.com/TheVarmax/TruckSim-Widget/releases/download/v1/TruckSimWidgetSetup.exe", false)]
    [InlineData("https://evil.com/TruckSimWidgetSetup.exe", false)]
    [InlineData("file:///C:/TruckSimWidgetSetup.exe", false)]
    [InlineData(@"C:\TruckSimWidgetSetup.exe", false)]
    [InlineData("--simulate-update", false)]
    public void ProductionReleaseAssetPolicyIsPreserved(string url, bool expected)
    {
        var policy = Updater.Assembly.GetType("TruckSimUpdater.ReleaseAssetPolicy", true)!;
        Assert.Equal(expected, policy.GetMethod("IsTrustedDownload", HiddenStatic)!
            .Invoke(null, new object[] { url, "TruckSimWidgetSetup.exe" }));
    }

    [Fact]
    public void ProductionDownloadStillStreamsHttpContent()
    {
        // Test transport on loopback separately from the unchanged GitHub allow-list.
        WithTempDirectory(dir =>
        {
            byte[] payload = Encoding.UTF8.GetBytes("production transport content");
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task server = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                await stream.ReadAsync(new byte[4096]);
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n"));
                await stream.WriteAsync(payload);
            });
            string destination = Path.Combine(dir, "download.exe");
            Download($"http://127.0.0.1:{port}/package.exe", destination);
            Assert.True(server.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(payload, File.ReadAllBytes(destination));
        });
    }

    [Fact]
    public void CompiledProductionFlowRetainsPolicyDownloadIntegrityAndLaunch()
    {
        Assert.Contains("TruckSimUpdater.ReleaseAssetPolicy::IsTrustedDownload", Calls(Updater.GetMethod("Main", HiddenStatic)!));
        var calls = Calls(AsyncBody(FormType, "RunUpdateAsync"));
        int download = calls.FindIndex(s => s.EndsWith("::DownloadWithProgressAsync"));
        int hash = calls.FindIndex(s => s.EndsWith("::ComputeHash"));
        int compare = calls.FindIndex(hash + 1, s => s == "System.String::Equals");
        int executable = calls.FindIndex(s => s.EndsWith("::IsValidExecutable"));
        int launch = calls.FindIndex(s => s == "System.Diagnostics.Process::Start");
        Assert.True(download >= 0 && hash > download && compare > hash && executable > compare && launch > executable);
        WithTempDirectory(dir =>
        {
            string package = Path.Combine(dir, "package.exe");
            byte[] bytes = new byte[1024 * 1024];
            bytes[0] = (byte)'M';
            bytes[1] = (byte)'Z';
            File.WriteAllBytes(package, bytes);
            var validate = Updater.GetMethod("IsValidExecutable", HiddenStatic)!;
            Assert.True((bool)validate.Invoke(null, new object[] { package })!);
            bytes[0] = 0;
            File.WriteAllBytes(package, bytes);
            Assert.False((bool)validate.Invoke(null, new object[] { package })!);
        });
    }

    private static MethodInfo AsyncBody(Type type, string name) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static IEnumerable<(OpCode Op, int Token)> Instructions(MethodInfo method)
    {
        byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;
        for (int offset = 0; offset < il.Length;)
        {
            short code = il[offset++];
            if (code == 0xfe) code = (short)(0xfe00 | il[offset++]);
            var op = Opcodes[code];
            int size = op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
                _ => 4
            };
            yield return (op, size == 4 ? BitConverter.ToInt32(il, offset) : 0);
            offset += size;
        }
    }

    private static List<string> Calls(MethodInfo method) => Instructions(method)
        .Where(i => i.Op.OperandType == OperandType.InlineMethod)
        .Select(i => method.Module.ResolveMethod(i.Token, method.DeclaringType!.GetGenericArguments(), method.GetGenericArguments())!)
        .Select(m => m.DeclaringType!.FullName + "::" + m.Name).ToList();

    private static IEnumerable<string> Strings(MethodInfo method) => Instructions(method)
        .Where(i => i.Op == OpCodes.Ldstr).Select(i => method.Module.ResolveString(i.Token));

    private static void Download(string url, string destination)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = (System.Windows.Forms.Form)Activator.CreateInstance(FormType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new object[] { url, "TruckSimWidgetSetup.exe", Path.GetDirectoryName(destination)!, "Widget.exe", "", "en", new string('a', 64) }, null)!;
                _ = form.Handle; // Create a hidden handle, without Shown/RunUpdateAsync or installer execution.
                var task = (Task)FormType.GetMethod("DownloadWithProgressAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(form, new object[] { url, destination })!;
                var timeout = System.Diagnostics.Stopwatch.StartNew();
                while (!task.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(15))
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(1);
                }
                Assert.True(task.IsCompleted, "Download timed out");
                task.GetAwaiter().GetResult();
            }
            catch (Exception ex) { error = ex; }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Download thread timed out");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    private static void WithTempDirectory(Action<string> action)
    {
        string path = Directory.CreateTempSubdirectory("TruckSimWidget_I17_Test_").FullName;
        try { action(path); }
        finally { Directory.Delete(path, true); }
    }
}
#endif
