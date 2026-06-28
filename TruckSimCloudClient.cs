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
        [JsonPropertyName("licenseKey")]
        public string LicenseKey { get; set; } = string.Empty;

        [JsonPropertyName("hardwareHash")]
        public string HardwareHash { get; set; } = string.Empty;

        [JsonPropertyName("appVersion")]
        public string AppVersion { get; set; } = string.Empty;
    }

    public class LicenseDeactivationRequest
    {
        [JsonPropertyName("licenseKey")]
        public string LicenseKey { get; set; } = string.Empty;

        [JsonPropertyName("hardwareHash")]
        public string HardwareHash { get; set; } = string.Empty;
    }

    public class LicenseResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

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

        [JsonPropertyName("expiresAt")]
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
            
            // Allow reading JSON on 400 Bad Request if the API returns validation errors in the same format
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.BadRequest)
            {
                response.EnsureSuccessStatusCode();
            }

            return await response.Content.ReadFromJsonAsync<LicenseResponse>();
        }

        public async Task<LicenseResponse?> CheckAsync(LicenseCheckRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BASE_URL}/license/check", request);
            
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.BadRequest)
            {
                response.EnsureSuccessStatusCode();
            }

            return await response.Content.ReadFromJsonAsync<LicenseResponse>();
        }

        public async Task<LicenseResponse?> DeactivateAsync(LicenseDeactivationRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{BASE_URL}/license/deactivate", request);
            
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.BadRequest)
            {
                response.EnsureSuccessStatusCode();
            }

            return await response.Content.ReadFromJsonAsync<LicenseResponse>();
        }
    }
}
