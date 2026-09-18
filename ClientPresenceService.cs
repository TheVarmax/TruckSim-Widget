using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ETSOverlay
{
    /// <summary>
    /// Manages client presence reporting (registration, 45s heartbeat, diagnostics, graceful shutdown).
    /// Protects against heartbeat/shutdown race conditions using SemaphoreSlim and cancellation tokens.
    /// </summary>
    public class ClientPresenceService
    {
        public static ClientPresenceService Instance { get; } = new ClientPresenceService();

        private readonly ITruckSimCloudClient _client;
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private readonly List<ClientDiagnosticEvent> _events = new();
        private readonly object _eventLock = new();
        private readonly object _stateLock = new();

        public int EventQueueCount
        {
            get
            {
                lock (_eventLock)
                {
                    return _events.Count;
                }
            }
        }

        private CancellationTokenSource? _heartbeatCts;
        private Task? _heartbeatLoopTask;
        private volatile bool _isShutDown = false;
        private volatile bool _isRegistered = false;

        private ClientStateInfo _currentLiveState = new();

        public bool IsRegistered => _isRegistered;
        public bool IsShutDown => _isShutDown;

        public ClientPresenceService(ITruckSimCloudClient? client = null)
        {
            _client = client ?? new TruckSimCloudClient();
        }

        #region Win32 Native Helpers

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVMODE
        {
            private const int CCHDEVICENAME = 32;
            private const int CCHFORMNAME = 32;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        #endregion

        #region Hardware & Software Inventory Gathering

        public ClientRegisterRequest GatherRegisterPayload()
        {
            return new ClientRegisterRequest
            {
                AppVersion = GetAppVersion(),
                Os = GetOsInfo(),
                Hardware = GetHardwareInfo(),
                Display = GetDisplayInfo(),
                Software = GetSoftwareInfo()
            };
        }

        public string GetAppVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.3";
        }

        private ClientOsInfo GetOsInfo()
        {
            string name = "Windows";
            string version = Environment.OSVersion.Version.ToString();
            string build = Environment.OSVersion.Version.Build.ToString();
            try
            {
                using var cv = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (cv != null)
                {
                    var productName = cv.GetValue("ProductName")?.ToString();
                    var displayVersion = cv.GetValue("DisplayVersion")?.ToString();
                    var currentBuild = cv.GetValue("CurrentBuild")?.ToString();
                    if (!string.IsNullOrWhiteSpace(currentBuild)) build = currentBuild;
                    if (!string.IsNullOrWhiteSpace(productName))
                    {
                        name = productName;
                        if (Environment.OSVersion.Version.Build >= 22000 && name.Contains("Windows 10"))
                        {
                            name = name.Replace("Windows 10", "Windows 11");
                        }
                        if (!string.IsNullOrWhiteSpace(displayVersion))
                        {
                            name += $" ({displayVersion})";
                        }
                    }
                }
            }
            catch { }

            return new ClientOsInfo
            {
                Name = name,
                Version = version,
                Build = build,
                Architecture = RuntimeInformation.OSArchitecture.ToString()
            };
        }

        private ClientHardwareInfo GetHardwareInfo()
        {
            string? cpu = null;
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                cpu = key?.GetValue("ProcessorNameString")?.ToString()?.Trim();
            }
            catch { }

            string? gpu = null;
            string? gpuDriver = null;
            try
            {
                // Scan video controllers in registry: {4d36e968-e325-11ce-bfc1-08002be10318}
                for (int i = 0; i <= 8; i++)
                {
                    string subKey = $@"SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\{i:D4}";
                    using var key = Registry.LocalMachine.OpenSubKey(subKey);
                    if (key != null)
                    {
                        var desc = key.GetValue("DriverDesc")?.ToString();
                        var driverVer = key.GetValue("DriverVersion")?.ToString();
                        if (!string.IsNullOrWhiteSpace(desc))
                        {
                            bool isDiscrete = desc.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                                              desc.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
                                              desc.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                                              desc.Contains("RTX", StringComparison.OrdinalIgnoreCase) ||
                                              desc.Contains("GTX", StringComparison.OrdinalIgnoreCase) ||
                                              desc.Contains("Arc", StringComparison.OrdinalIgnoreCase);

                            if (gpu == null || isDiscrete)
                            {
                                gpu = desc.Trim();
                                gpuDriver = driverVer?.Trim();
                                if (isDiscrete) break;
                            }
                        }
                    }
                }
            }
            catch { }

            int? ramMb = null;
            try
            {
                var mem = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(mem))
                {
                    ramMb = (int)(mem.ullTotalPhys / (1024 * 1024));
                }
            }
            catch { }

            return new ClientHardwareInfo
            {
                Cpu = cpu,
                Gpu = gpu,
                GpuDriver = gpuDriver,
                RamMb = ramMb
            };
        }

        private ClientDisplayInfo GetDisplayInfo()
        {
            int width = 0;
            int height = 0;
            int refreshRate = 0;

            try
            {
                var dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                if (EnumDisplaySettings(null, -1, ref dm))
                {
                    width = dm.dmPelsWidth;
                    height = dm.dmPelsHeight;
                    refreshRate = dm.dmDisplayFrequency;
                }
            }
            catch { }

            if (width <= 0 || height <= 0)
            {
                try
                {
                    width = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
                    height = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
                    refreshRate = 60;
                }
                catch { }
            }

            return new ClientDisplayInfo
            {
                Width = width > 0 ? width : null,
                Height = height > 0 ? height : null,
                RefreshRate = refreshRate > 0 ? refreshRate : null
            };
        }

        private ClientSoftwareInfo? _cachedSoftwareInfo;

        public ClientSoftwareInfo GetSoftwareInfo()
        {
            if (_cachedSoftwareInfo != null) return _cachedSoftwareInfo;

            string? ets2Ver = GetGameExeVersion(CityTranslationExtractor.Ets2AppId, "Euro Truck Simulator 2", "eurotrucks2.exe");
            string? atsVer = GetGameExeVersion(CityTranslationExtractor.AtsAppId, "American Truck Simulator", "amtrucks.exe");
            string? tmpVer = GetTruckersMpVersion();
            string runtimeVer = RuntimeInformation.FrameworkDescription;

            _cachedSoftwareInfo = new ClientSoftwareInfo
            {
                Ets2Version = ets2Ver,
                AtsVersion = atsVer,
                TruckersMpVersion = tmpVer,
                RuntimeVersion = runtimeVer
            };
            return _cachedSoftwareInfo;
        }

        public string? Ets2Version => GetSoftwareInfo().Ets2Version;
        public string? AtsVersion => GetSoftwareInfo().AtsVersion;

        private string? GetGameExeVersion(int appId, string folderName, string exeName)
        {
            try
            {
                string? installDir = CityTranslationExtractor.FindGameInstallation(appId, folderName);
                if (!string.IsNullOrEmpty(installDir))
                {
                    string exe64 = Path.Combine(installDir, "bin", "win_x64", exeName);
                    if (File.Exists(exe64))
                    {
                        var info = FileVersionInfo.GetVersionInfo(exe64);
                        return info.FileVersion ?? info.ProductVersion;
                    }
                    string exe86 = Path.Combine(installDir, "bin", "win_x86", exeName);
                    if (File.Exists(exe86))
                    {
                        var info = FileVersionInfo.GetVersionInfo(exe86);
                        return info.FileVersion ?? info.ProductVersion;
                    }
                }
            }
            catch { }
            return null;
        }

        private string? GetTruckersMpVersion()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string tmpData = Path.Combine(localAppData, "TruckersMP");
                if (Directory.Exists(tmpData))
                {
                    string launcherExe = Path.Combine(tmpData, "TruckersMP-Launcher.exe");
                    if (File.Exists(launcherExe))
                    {
                        return FileVersionInfo.GetVersionInfo(launcherExe).FileVersion;
                    }
                }
            }
            catch { }
            return null;
        }

        #endregion

        #region Presence Lifecycle & Heartbeat Loop

        public void Start()
        {
            lock (_stateLock)
            {
                if (_heartbeatLoopTask != null && !_heartbeatLoopTask.IsCompleted)
                {
                    return;
                }

                _isShutDown = false;
                _heartbeatCts = new CancellationTokenSource();
                _heartbeatLoopTask = Task.Run(() => HeartbeatLoopAsync(_heartbeatCts.Token));
            }
        }

        private async Task HeartbeatLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && !_isShutDown)
            {
                string token = DeviceTokenStorage.LoadToken();
                if (string.IsNullOrWhiteSpace(token))
                {
                    try { await Task.Delay(10000, ct); } catch (OperationCanceledException) { break; }
                    continue;
                }

                bool acquired = false;
                try
                {
                    await _syncLock.WaitAsync(ct);
                    acquired = true;

                    if (_isShutDown) break;

                    if (!_isRegistered)
                    {
                        var registerPayload = GatherRegisterPayload();
                        var regRes = await _client.RegisterClientAsync(token, registerPayload, ct);
                        if (regRes != null && regRes.Success)
                        {
                            _isRegistered = true;
                            RecordEvent("widget_started", "TruckSim Widget started");
                        }
                    }

                    if (_isRegistered)
                    {
                        await FlushEventsInternalAsync(token, ct);

                        ClientStateInfo stateSnapshot;
                        lock (_stateLock)
                        {
                            stateSnapshot = new ClientStateInfo
                            {
                                GameRunning = _currentLiveState.GameRunning,
                                Game = _currentLiveState.Game,
                                GameVersion = _currentLiveState.GameVersion,
                                TelemetryConnected = _currentLiveState.TelemetryConnected,
                                TrucksBookConnected = _currentLiveState.TrucksBookConnected,
                                TrackingActive = _currentLiveState.TrackingActive,
                                RecordingActive = _currentLiveState.RecordingActive,
                                SyncActive = _currentLiveState.SyncActive
                            };
                        }

                        var hbReq = new ClientHeartbeatRequest
                        {
                            AppVersion = GetAppVersion(),
                            State = stateSnapshot
                        };

                        var hbRes = await _client.SendHeartbeatAsync(token, hbReq, ct);
                        if (hbRes != null)
                        {
                            if (hbRes.Error == "device_offline")
                            {
                                _isRegistered = false;
                            }
                            else if (hbRes.Error == "device_inactive" || hbRes.Error == "device_blocked" || hbRes.Error == "license_inactive")
                            {
                                _isRegistered = false;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ClientPresence] Heartbeat loop error: {ex.Message}");
                }
                finally
                {
                    if (acquired) _syncLock.Release();
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(45), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public async Task ShutdownAsync(string reason = "user_exit", int timeoutMs = 2500)
        {
            if (_isShutDown) return;

            try
            {
                _heartbeatCts?.Cancel();
            }
            catch { }

            // 1. Acquire lock with a short timeout to prevent blocking shutdown indefinitely
            using (var lockCts = new CancellationTokenSource(Math.Min(1000, timeoutMs)))
            {
                try
                {
                    await _syncLock.WaitAsync(lockCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _isShutDown = true;
                    _isRegistered = false;
                    return;
                }
            }

            try
            {
                if (_isShutDown) return;

                _isShutDown = true;
                _isRegistered = false;

                string token = DeviceTokenStorage.LoadToken();
                if (string.IsNullOrWhiteSpace(token)) return;

                // 2. Best-effort short timeout budget strictly for diagnostic flush (max 800ms)
                // Prevents slow/hanging event requests from starving the shutdown request.
                int flushTimeoutMs = Math.Min(800, Math.Max(200, timeoutMs / 3));
                using (var flushCts = new CancellationTokenSource(flushTimeoutMs))
                {
                    try
                    {
                        await FlushEventsInternalAsync(token, flushCts.Token);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ClientPresence] Shutdown flush diagnostics non-critical: {ex.Message}");
                    }
                }

                // 3. Guaranteed separate timeout budget strictly for /client/shutdown
                int shutdownTimeoutMs = Math.Max(1500, timeoutMs);
                using (var shutdownCts = new CancellationTokenSource(shutdownTimeoutMs))
                {
                    var shutdownReq = new ClientShutdownRequest
                    {
                        Reason = reason,
                        AppVersion = GetAppVersion()
                    };

                    await _client.SendShutdownAsync(token, shutdownReq, shutdownCts.Token);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClientPresence] Shutdown exception: {ex.Message}");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        #endregion

        #region Live State & Diagnostic Events

        public void UpdateLiveState(Action<ClientStateInfo> updater)
        {
            lock (_stateLock)
            {
                updater(_currentLiveState);
            }
        }

        public void RecordEvent(string type, string? message = null, Dictionary<string, object>? metadata = null)
        {
            if (_isShutDown) return;

            Dictionary<string, object>? safeMeta = null;
            if (metadata != null && metadata.Count > 0)
            {
                safeMeta = new Dictionary<string, object>();
                foreach (var kvp in metadata)
                {
                    string k = kvp.Key.ToLowerInvariant();
                    if (k.Contains("token") || k.Contains("secret") || k.Contains("password") || k.Contains("key") || k.Contains("path"))
                        continue;

                    if (kvp.Value is string s)
                    {
                        safeMeta[kvp.Key] = s.Length > 200 ? s.Substring(0, 200) : s;
                    }
                    else if (kvp.Value is int or long or float or double or bool)
                    {
                        safeMeta[kvp.Key] = kvp.Value;
                    }
                }
            }

            var evt = new ClientDiagnosticEvent
            {
                Type = type,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Message = message != null && message.Length > 256 ? message.Substring(0, 256) : message,
                Metadata = safeMeta
            };

            lock (_eventLock)
            {
                while (_events.Count >= 100)
                {
                    _events.RemoveAt(0);
                }
                _events.Add(evt);
            }
        }

        internal Task FlushEventsAsync(string token, CancellationToken ct = default)
        {
            return FlushEventsInternalAsync(token, ct);
        }

        private async Task FlushEventsInternalAsync(string token, CancellationToken ct)
        {
            List<ClientDiagnosticEvent> batch;
            lock (_eventLock)
            {
                if (_events.Count == 0) return;
                batch = _events.Take(50).ToList();
            }

            if (batch.Count == 0) return;

            try
            {
                var res = await _client.SendEventsAsync(token, new ClientEventsRequest { Events = batch }, ct);
                if (res != null && res.Success)
                {
                    lock (_eventLock)
                    {
                        int countToRemove = Math.Min(batch.Count, _events.Count);
                        _events.RemoveRange(0, countToRemove);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClientPresence] Failed to send events: {ex.Message}");
                // Network failure: events are preserved in _events in original order
            }
        }

        #endregion
    }
}
