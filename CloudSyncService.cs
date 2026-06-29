using System;
using System.Threading.Tasks;

namespace ETSOverlay
{
    public class CloudSyncService
    {
        private readonly TruckSimCloudClient _apiClient;

        public CloudSyncService(TruckSimCloudClient apiClient)
        {
            _apiClient = apiClient;
        }

        private CloudSyncStatusRequest CreateBaseRequest(string version)
        {
            return new CloudSyncStatusRequest
            {
                DeviceToken = LicenseManager.Instance.DeviceToken,
                HardwareHash = LicenseManager.Instance.HardwareHash,
                AppVersion = version
            };
        }

        public async Task<CloudSyncResponse?> GetStatusAsync(string version)
        {
            var req = CreateBaseRequest(version);
            return await _apiClient.GetSyncStatusAsync(req);
        }

        public async Task<CloudSyncResponse?> GetSettingsAsync(string version)
        {
            var req = CreateBaseRequest(version);
            return await _apiClient.GetSyncSettingsAsync(req);
        }

        public async Task<CloudSyncResponse?> SaveSettingsAsync(string version, int? revision, CloudSyncSettings settings)
        {
            var req = new CloudSyncSettingsRequest
            {
                DeviceToken = LicenseManager.Instance.DeviceToken,
                HardwareHash = LicenseManager.Instance.HardwareHash,
                AppVersion = version,
                SchemaVersion = 1,
                Revision = revision,
                Settings = settings
            };
            return await _apiClient.SaveSyncSettingsAsync(req);
        }

        public async Task<CloudSyncResponse?> DeleteSettingsAsync(string version)
        {
            var req = CreateBaseRequest(version);
            return await _apiClient.DeleteSyncSettingsAsync(req);
        }
    }
}
