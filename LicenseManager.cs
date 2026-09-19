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

        private string _deviceToken = string.Empty;
        public string DeviceToken => _deviceToken;
        public bool HasValidToken => !string.IsNullOrEmpty(_deviceToken);
        public string HardwareHash { get; private set; } = string.Empty;
        public string CurrentPlan { get; private set; } = string.Empty;
        public string Source { get; private set; } = string.Empty;
        public string Status { get; private set; } = "inactive";
        public DateTime LastValidationTime { get; private set; }
        public DateTime? ExpiresAt { get; private set; }
        public bool HasValidatedThisSession { get; private set; } = false;
        public bool LastValidationFailed { get; private set; } = false;

        public event Action? OnLicenseChanged;

        private LicenseManager() { }

        public void LoadDeviceToken()
        {
            _deviceToken = DeviceTokenStorage.LoadToken();
        }

        private static void LogMessage(string message)
        {
            try
            {
                if (System.Windows.Application.Current?.MainWindow is MainWindow main)
                {
                    main.Dispatcher.Invoke(() => main.WriteLog(message));
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine(message);
                    Console.Error.WriteLine(message);
                }
            }
            catch
            {
                System.Diagnostics.Trace.WriteLine(message);
            }
        }

        public void Initialize(string hardwareHash, List<string>? cachedFeatures, DateTime lastValidationTime, string plan, string source, string status, DateTime? expiresAt = null)
        {
            LoadDeviceToken();
            
            // Generate device ID if not present
            if (string.IsNullOrWhiteSpace(hardwareHash))
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                    var guid = key?.GetValue("MachineGuid")?.ToString();
                    HardwareHash = !string.IsNullOrWhiteSpace(guid) ? guid : Guid.NewGuid().ToString();
                }
                catch
                {
                    HardwareHash = Guid.NewGuid().ToString();
                }
            }
            else
            {
                HardwareHash = hardwareHash;
            }

            if (!HasValidToken)
            {
                LogMessage("[LICENSE] Initialize: No device token found. License is set to inactive.");
                LastValidationTime = DateTime.MinValue;
                CurrentPlan = "";
                Source = "";
                Status = "inactive";
                ExpiresAt = null;
                _features.Clear();
            }
            else
            {
                LastValidationTime = lastValidationTime;
                CurrentPlan = plan ?? "";
                Source = source ?? "";
                Status = string.IsNullOrWhiteSpace(status) ? "active" : status;
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
                    
                    _deviceToken = response.DeviceToken ?? string.Empty;
                    if (!string.IsNullOrEmpty(_deviceToken))
                    {
                        DeviceTokenStorage.SaveToken(_deviceToken);
                    }
                    
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
            if (!HasValidToken)
            {
                LoadDeviceToken();
            }
            if (!HasValidToken)
            {
                if (Status != "inactive" || !string.IsNullOrEmpty(CurrentPlan))
                {
                    LogMessage("[LICENSE] Validation check skipped: missing device token. Deactivating local license state.");
                    ClearLicenseState();
                }
                return;
            }

            try
            {
                var request = new LicenseCheckRequest
                {
                    DeviceToken = DeviceToken,
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
                        // Explicitly reported invalid by server
                        LogMessage($"[LICENSE] Server rejected license check: {response.Message ?? "Invalid"}. Deactivating.");
                        ClearLicenseState();
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                LastValidationFailed = true;
                LogMessage($"[LICENSE] Validation HTTP error (offline mode): {ex.Message}");
            }
            catch (Exception ex)
            {
                LastValidationFailed = true;
                LogMessage($"[LICENSE] Validation error: {ex.Message}");
            }
        }

        public async Task<(bool success, string message)> DeactivateAsync()
        {
            if (!HasValidToken)
            {
                LogMessage("[LICENSE] Deactivate: No valid device token. Resetting local license state.");
                ClearLicenseState();
                return (true, "License state reset.");
            }

            try
            {
                var request = new LicenseDeactivationRequest
                {
                    DeviceToken = DeviceToken,
                    HardwareHash = HardwareHash
                };

                var response = await _client.DeactivateAsync(request);

                if (response != null && response.Success)
                {
                    ClearLicenseState();
                    return (true, "Deactivated successfully.");
                }

                // If server explicitly said token is invalid, also clear local state
                ClearLicenseState();
                return (true, response?.Message ?? "Deactivated locally.");
            }
            catch (Exception)
            {
                ClearLicenseState();
                return (true, "Deactivated locally (server unreachable).");
            }
        }

        public async Task<(bool success, string message, string url)> CreatePortalSessionAsync()
        {
            if (!HasValidToken) return (false, "Not authenticated", "");

            try
            {
                var request = new PortalSessionRequest
                {
                    DeviceToken = DeviceToken
                };

                var response = await _client.CreatePortalSessionAsync(request);

                if (response != null && response.Success && !string.IsNullOrEmpty(response.Url))
                {
                    return (true, "", response.Url);
                }

                return (false, response?.Message ?? "Failed to create portal session.", "");
            }
            catch (Exception)
            {
                return (false, "Unable to contact the license server. Please try again later.", "");
            }
        }

        public event Action<List<string>, bool>? OnFeaturesValidated;

        private void UpdateStateFromResponse(LicenseResponse response)
        {
            Status = response.License?.Status ?? "active";
            CurrentPlan = response.License?.Plan ?? "";
            Source = response.License?.Source ?? "";
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

        public void ClearLicenseState()
        {
            LogMessage("[LICENSE] Deactivating license and clearing local credentials.");
            _deviceToken = string.Empty;
            DeviceTokenStorage.DeleteToken();
            CurrentPlan = string.Empty;
            Source = string.Empty;
            Status = "inactive";
            ExpiresAt = null;
            _features.Clear();
            OnFeaturesValidated?.Invoke(new List<string>(), false);
            OnLicenseChanged?.Invoke();
        }
    }
}
