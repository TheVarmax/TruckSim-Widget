using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ETSOverlay
{
    public class LicenseActivationRequest
    {
        [JsonPropertyName("licenseKey")]
        public string LicenseKey { get; set; } = string.Empty;

        [JsonPropertyName("hardwareHash")]
        public string HardwareHash { get; set; } = string.Empty;

        [JsonPropertyName("deviceName")]
        public string DeviceName { get; set; } = string.Empty;

        [JsonPropertyName("appVersion")]
        public string AppVersion { get; set; } = string.Empty;
    }

    public class LicenseCheckRequest
    {
        [JsonPropertyName("deviceToken")]
        public string DeviceToken { get; set; } = string.Empty;

        [JsonPropertyName("hardwareHash")]
        public string HardwareHash { get; set; } = string.Empty;

        [JsonPropertyName("appVersion")]
        public string AppVersion { get; set; } = string.Empty;
    }

    public class LicenseDeactivationRequest
    {
        [JsonPropertyName("deviceToken")]
        public string DeviceToken { get; set; } = string.Empty;

        [JsonPropertyName("hardwareHash")]
        public string HardwareHash { get; set; } = string.Empty;
    }

    public class PortalSessionRequest
    {
        [JsonPropertyName("device_token")]
        public string DeviceToken { get; set; } = string.Empty;
    }

    public class PortalSessionResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
        
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    public class LicenseResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("deviceToken")]
        public string? DeviceToken { get; set; }

        [JsonPropertyName("license")]
        public LicenseInfo? License { get; set; }

        [JsonPropertyName("device")]
        public DeviceInfo? Device { get; set; }

        [JsonPropertyName("features")]
        public List<string>? Features { get; set; }
        
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    public class LicenseInfo
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("plan")]
        public string Plan { get; set; } = string.Empty;
        
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("validUntil")]
        public DateTime? ExpiresAt { get; set; }
    }

    public class DeviceInfo
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    public class TruckSimCloudClient : ITruckSimCloudClient
    {
        private const string BASE_URL = "https://api.trucksim.uk";
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<LicenseResponse?> ActivateAsync(LicenseActivationRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BASE_URL}/license/activate", request, cancellationToken);
            
            // Allow reading JSON on 4xx errors if the API returns validation/revocation errors in the same format
            if (!response.IsSuccessStatusCode && (int)response.StatusCode >= 500)
            {
                response.EnsureSuccessStatusCode();
            }

            string rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                if (System.Windows.Application.Current?.MainWindow is MainWindow main)
                {
                    main.Dispatcher.Invoke(() => main.WriteLog($"[API] /license/activate RAW JSON: {rawJson}"));
                }
            }
            catch { }

            return System.Text.Json.JsonSerializer.Deserialize<LicenseResponse>(rawJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<LicenseResponse?> CheckAsync(LicenseCheckRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BASE_URL}/license/check", request, cancellationToken);
            
            if (!response.IsSuccessStatusCode && (int)response.StatusCode >= 500)
            {
                response.EnsureSuccessStatusCode();
            }

            string rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                if (System.Windows.Application.Current?.MainWindow is MainWindow main)
                {
                    main.Dispatcher.Invoke(() => main.WriteLog($"[API] /license/check RAW JSON: {rawJson}"));
                }
            }
            catch { }

            try
            {
                var result = System.Text.Json.JsonSerializer.Deserialize<LicenseResponse>(rawJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (result == null && !response.IsSuccessStatusCode)
                {
                    return new LicenseResponse { Success = false, Message = "HTTP Error " + response.StatusCode };
                }
                return result;
            }
            catch (System.Text.Json.JsonException)
            {
                if (!response.IsSuccessStatusCode)
                {
                    return new LicenseResponse { Success = false, Message = "HTTP Error " + response.StatusCode };
                }
                throw;
            }
        }

        public async Task<LicenseResponse?> DeactivateAsync(LicenseDeactivationRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BASE_URL}/license/deactivate", request, cancellationToken);
            
            if (!response.IsSuccessStatusCode && (int)response.StatusCode >= 500)
            {
                response.EnsureSuccessStatusCode();
            }

            return await response.Content.ReadFromJsonAsync<LicenseResponse>(cancellationToken: cancellationToken);
        }

        public async Task<PortalSessionResponse?> CreatePortalSessionAsync(PortalSessionRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BASE_URL}/stripe/create-portal-session", request, cancellationToken);
            
            if (!response.IsSuccessStatusCode && (int)response.StatusCode >= 500)
            {
                response.EnsureSuccessStatusCode();
            }

            return await response.Content.ReadFromJsonAsync<PortalSessionResponse>(cancellationToken: cancellationToken);
        }

        public async Task<CloudSyncResponse?> GetSyncStatusAsync(CloudSyncStatusRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BASE_URL}/sync/status", request);
            if (!response.IsSuccessStatusCode && (int)response.StatusCode >= 500)
                response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CloudSyncResponse>();
        }

        public async Task<CloudSyncResponse?> GetSyncSettingsAsync(CloudSyncStatusRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BASE_URL}/sync/settings", request);
            if (!response.IsSuccessStatusCode && (int)response.StatusCode >= 500)
                response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CloudSyncResponse>();
        }

        public async Task<CloudSyncResponse?> SaveSyncSettingsAsync(CloudSyncSettingsRequest request)
        {
            var response = await _httpClient.PutAsJsonAsync($"{BASE_URL}/sync/settings", request);
            if (!response.IsSuccessStatusCode && (int)response.StatusCode >= 500)
                response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CloudSyncResponse>();
        }

        public async Task<CloudSyncResponse?> DeleteSyncSettingsAsync(CloudSyncStatusRequest request)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{BASE_URL}/sync/settings")
            {
                Content = JsonContent.Create(request)
            };
            var response = await _httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode && (int)response.StatusCode >= 500)
                response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CloudSyncResponse>();
        }

        private async Task<ClientApiResponse?> SendClientPostAsync<T>(string endpoint, string deviceToken, T body, System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"{BASE_URL}{endpoint}")
                {
                    Content = JsonContent.Create(body)
                };
                req.Headers.Add("X-Device-Token", deviceToken);

                var response = await _httpClient.SendAsync(req, cancellationToken);
                string rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

                return System.Text.Json.JsonSerializer.Deserialize<ClientApiResponse>(
                    rawJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (System.OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new ClientApiResponse
                {
                    Success = false,
                    Error = "network_error",
                    Message = ex.Message
                };
            }
        }

        public async Task<ClientApiResponse?> RegisterClientAsync(string deviceToken, ClientRegisterRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            return await SendClientPostAsync("/client/register", deviceToken, request, cancellationToken);
        }

        public async Task<ClientApiResponse?> SendHeartbeatAsync(string deviceToken, ClientHeartbeatRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            return await SendClientPostAsync("/client/heartbeat", deviceToken, request, cancellationToken);
        }

        public async Task<ClientApiResponse?> SendShutdownAsync(string deviceToken, ClientShutdownRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            return await SendClientPostAsync("/client/shutdown", deviceToken, request, cancellationToken);
        }

        public async Task<ClientApiResponse?> SendEventsAsync(string deviceToken, ClientEventsRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            return await SendClientPostAsync("/client/events", deviceToken, request, cancellationToken);
        }
    }

    public interface ITruckSimCloudClient
    {
        Task<ClientApiResponse?> RegisterClientAsync(string deviceToken, ClientRegisterRequest request, System.Threading.CancellationToken cancellationToken = default);
        Task<ClientApiResponse?> SendHeartbeatAsync(string deviceToken, ClientHeartbeatRequest request, System.Threading.CancellationToken cancellationToken = default);
        Task<ClientApiResponse?> SendShutdownAsync(string deviceToken, ClientShutdownRequest request, System.Threading.CancellationToken cancellationToken = default);
        Task<ClientApiResponse?> SendEventsAsync(string deviceToken, ClientEventsRequest request, System.Threading.CancellationToken cancellationToken = default);
    }

    public class ClientOsInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("build")]
        public string? Build { get; set; }

        [JsonPropertyName("architecture")]
        public string? Architecture { get; set; }
    }

    public class ClientHardwareInfo
    {
        [JsonPropertyName("cpu")]
        public string? Cpu { get; set; }

        [JsonPropertyName("gpu")]
        public string? Gpu { get; set; }

        [JsonPropertyName("gpuDriver")]
        public string? GpuDriver { get; set; }

        [JsonPropertyName("ramMb")]
        public int? RamMb { get; set; }
    }

    public class ClientDisplayInfo
    {
        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("refreshRate")]
        public int? RefreshRate { get; set; }
    }

    public class ClientSoftwareInfo
    {
        [JsonPropertyName("ets2Version")]
        public string? Ets2Version { get; set; }

        [JsonPropertyName("atsVersion")]
        public string? AtsVersion { get; set; }

        [JsonPropertyName("truckersMpVersion")]
        public string? TruckersMpVersion { get; set; }

        [JsonPropertyName("runtimeVersion")]
        public string? RuntimeVersion { get; set; }
    }

    public class ClientRegisterRequest
    {
        [JsonPropertyName("appVersion")]
        public string? AppVersion { get; set; }

        [JsonPropertyName("os")]
        public ClientOsInfo? Os { get; set; }

        [JsonPropertyName("hardware")]
        public ClientHardwareInfo? Hardware { get; set; }

        [JsonPropertyName("display")]
        public ClientDisplayInfo? Display { get; set; }

        [JsonPropertyName("software")]
        public ClientSoftwareInfo? Software { get; set; }
    }

    public class ClientStateInfo
    {
        [JsonPropertyName("gameRunning")]
        public bool GameRunning { get; set; }

        [JsonPropertyName("game")]
        public string? Game { get; set; }

        [JsonPropertyName("gameVersion")]
        public string? GameVersion { get; set; }

        [JsonPropertyName("telemetryConnected")]
        public bool TelemetryConnected { get; set; }

        [JsonPropertyName("trucksBookConnected")]
        public bool TrucksBookConnected { get; set; }

        [JsonPropertyName("trackingActive")]
        public bool TrackingActive { get; set; }

        [JsonPropertyName("recordingActive")]
        public bool RecordingActive { get; set; }

        [JsonPropertyName("syncActive")]
        public bool SyncActive { get; set; }
    }

    public class ClientHeartbeatRequest
    {
        [JsonPropertyName("appVersion")]
        public string? AppVersion { get; set; }

        [JsonPropertyName("state")]
        public ClientStateInfo? State { get; set; }
    }

    public class ClientShutdownRequest
    {
        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("appVersion")]
        public string? AppVersion { get; set; }
    }

    public class ClientDiagnosticEvent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class ClientEventsRequest
    {
        [JsonPropertyName("events")]
        public List<ClientDiagnosticEvent> Events { get; set; } = new();
    }

    public class ClientApiResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("alreadyShutdown")]
        public bool? AlreadyShutdown { get; set; }
    }
}
