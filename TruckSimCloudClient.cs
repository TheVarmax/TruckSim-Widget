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

        [JsonPropertyName("validUntil")]
        public DateTime? ExpiresAt { get; set; }
    }

    public class DeviceInfo
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    public class TruckSimCloudClient
    {
        private const string BASE_URL = "https://trucksim-cloud-api.sd4nxwfsrb.workers.dev";
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<LicenseResponse?> ActivateAsync(LicenseActivationRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BASE_URL}/license/activate", request);
            
            // Allow reading JSON on 4xx errors if the API returns validation/revocation errors in the same format
            if (!response.IsSuccessStatusCode && (int)response.StatusCode >= 500)
            {
                response.EnsureSuccessStatusCode();
            }

            string rawJson = await response.Content.ReadAsStringAsync();
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

        public async Task<LicenseResponse?> CheckAsync(LicenseCheckRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BASE_URL}/license/check", request);
            
            if (!response.IsSuccessStatusCode && (int)response.StatusCode >= 500)
            {
                response.EnsureSuccessStatusCode();
            }

            string rawJson = await response.Content.ReadAsStringAsync();
            try
            {
                if (System.Windows.Application.Current?.MainWindow is MainWindow main)
                {
                    main.Dispatcher.Invoke(() => main.WriteLog($"[API] /license/check RAW JSON: {rawJson}"));
                }
            }
            catch { }

            return System.Text.Json.JsonSerializer.Deserialize<LicenseResponse>(rawJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<LicenseResponse?> DeactivateAsync(LicenseDeactivationRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BASE_URL}/license/deactivate", request);
            
            if (!response.IsSuccessStatusCode && (int)response.StatusCode >= 500)
            {
                response.EnsureSuccessStatusCode();
            }

            return await response.Content.ReadFromJsonAsync<LicenseResponse>();
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
    }
}
