using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace ETSOverlay
{
    public class LicenseManager
    {
        public static LicenseManager Instance { get; } = new LicenseManager();

        private readonly TruckSimCloudClient _client = new TruckSimCloudClient();
        private readonly HashSet<string> _features = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public string LicenseKey { get; private set; } = string.Empty;
        public string HardwareHash { get; private set; } = string.Empty;
        public string CurrentPlan { get; private set; } = string.Empty;
        public string Status { get; private set; } = "inactive";
        public DateTime LastValidationTime { get; private set; }
        public DateTime? ExpiresAt { get; private set; }
        public bool HasValidatedThisSession { get; private set; } = false;
        public bool LastValidationFailed { get; private set; } = false;

        public event Action? OnLicenseChanged;

        private LicenseManager() { }

        public void Initialize(string licenseKey, string hardwareHash, List<string>? cachedFeatures, DateTime lastValidationTime, string plan, string status, DateTime? expiresAt = null)
        {
            LicenseKey = licenseKey ?? string.Empty;
            
            // Generate device ID if not present
            if (string.IsNullOrWhiteSpace(hardwareHash))
            {
                HardwareHash = Guid.NewGuid().ToString();
            }
            else
            {
                HardwareHash = hardwareHash;
            }

            LastValidationTime = lastValidationTime;
            CurrentPlan = plan ?? "";
            Status = string.IsNullOrWhiteSpace(status) ? "inactive" : status;
            ExpiresAt = expiresAt;

            _features.Clear();
            if (cachedFeatures != null)
            {
                foreach (var f in cachedFeatures)
                {
                    _features.Add(f);
                }
            }
        }

        public bool HasFeature(string feature)
        {
            return _features.Contains(feature);
        }

        public List<string> GetFeaturesList()
        {
            return new List<string>(_features);
        }

        public async Task<(bool success, string message)> ActivateAsync(string key, string appVersion)
        {
            try
            {
                var request = new LicenseActivationRequest
                {
                    LicenseKey = key,
                    HardwareHash = HardwareHash,
                    DeviceName = Environment.MachineName,
                    AppVersion = appVersion
                };

                var response = await _client.ActivateAsync(request);
                
                if (response != null && response.Success && response.License != null)
                {
                    HasValidatedThisSession = true;
                    LastValidationFailed = false;
                    LicenseKey = key;
                    UpdateStateFromResponse(response);
                    return (true, "Activated successfully.");
                }
                
                HasValidatedThisSession = true;
                LastValidationFailed = false;
                
                return (false, response?.Message ?? "Failed to activate. Please check your key.");
            }
            catch (Exception)
            {
                LastValidationFailed = true;
                return (false, "Unable to contact the license server. Please try again later.");
            }
        }

        public async Task ValidateLicenseAsync(string appVersion)
        {
            if (string.IsNullOrWhiteSpace(LicenseKey)) return;

            try
            {
                var request = new LicenseCheckRequest
                {
                    LicenseKey = LicenseKey,
                    HardwareHash = HardwareHash,
                    AppVersion = appVersion
                };

                var response = await _client.CheckAsync(request);

                if (response != null)
                {
                    HasValidatedThisSession = true;
                    LastValidationFailed = false;
                    if (response.Success && response.License != null)
                    {
                        UpdateStateFromResponse(response);
                    }
                    else
                    {
                        // Explicitly reported invalid
                        ClearLicenseState();
                    }
                }
            }
            catch (HttpRequestException)
            {
                LastValidationFailed = true;
                // Offline mode: Keep last known state
            }
            catch (Exception)
            {
                LastValidationFailed = true;
                // Other errors: Keep last known state
            }
        }

        public async Task<(bool success, string message)> DeactivateAsync()
        {
            if (string.IsNullOrWhiteSpace(LicenseKey)) return (true, "");

            try
            {
                var request = new LicenseDeactivationRequest
                {
                    LicenseKey = LicenseKey,
                    HardwareHash = HardwareHash
                };

                var response = await _client.DeactivateAsync(request);

                ClearLicenseState();
                
                if (response != null && response.Success)
                {
                    return (true, "Deactivated successfully.");
                }

                return (false, response?.Message ?? "Deactivated locally, server failed.");
            }
            catch (Exception)
            {
                ClearLicenseState();
                return (false, "Deactivated locally. Unable to contact the license server.");
            }
        }

        public event Action<List<string>, bool>? OnFeaturesValidated;

        private void UpdateStateFromResponse(LicenseResponse response)
        {
            Status = response.License?.Status ?? "active";
            CurrentPlan = response.License?.Plan ?? "";
            ExpiresAt = response.License?.ExpiresAt;
            LastValidationTime = DateTime.Now;

            _features.Clear();
            if (response.Features != null)
            {
                foreach (var f in response.Features)
                {
                    _features.Add(f);
                }
            }

            OnFeaturesValidated?.Invoke(GetFeaturesList(), HasFeature("cloud_sync"));
            OnLicenseChanged?.Invoke();
        }

        private void ClearLicenseState()
        {
            LicenseKey = string.Empty;
            CurrentPlan = string.Empty;
            Status = "inactive";
            ExpiresAt = null;
            _features.Clear();
            OnLicenseChanged?.Invoke();
        }
    }
}
