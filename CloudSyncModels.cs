using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ETSOverlay
{
    public class CloudSyncSettings
    {
        [JsonPropertyName("uiMode")]
        public string UiMode { get; set; } = "full";
        [JsonPropertyName("windowOpacity")]
        public double WindowOpacity { get; set; } = 0.85;
        [JsonPropertyName("uiLanguage")]
        public string UiLanguage { get; set; } = "en";
        [JsonPropertyName("autoHideEnabled")]
        public bool AutoHideEnabled { get; set; } = false;
        [JsonPropertyName("uiScale")]
        public int UiScale { get; set; } = 100;
        [JsonPropertyName("speedWarningEts")]
        public int SpeedWarningEts { get; set; }
        [JsonPropertyName("speedWarningAts")]
        public int SpeedWarningAts { get; set; }
        [JsonPropertyName("savedTheme")]
        public string SavedTheme { get; set; } = "classic";
        [JsonPropertyName("savedAccent")]
        public string SavedAccent { get; set; } = "teal";
        [JsonPropertyName("savedCardStyle")]
        public string SavedCardStyle { get; set; } = "standard";
        [JsonPropertyName("accentMode")]
        public string AccentMode { get; set; } = "standard";
        [JsonPropertyName("customCardAccents")]
        public Dictionary<string, string> CustomCardAccents { get; set; } = new();
        [JsonPropertyName("skipBetaUpdates")]
        public bool SkipBetaUpdates { get; set; } = false;
    }

    public class CloudSyncStatusRequest
    {
        [JsonPropertyName("licenseKey")]
        public string LicenseKey { get; set; } = string.Empty;
        [JsonPropertyName("hardwareHash")]
        public string HardwareHash { get; set; } = string.Empty;
        [JsonPropertyName("appVersion")]
        public string AppVersion { get; set; } = string.Empty;
    }

    public class CloudSyncSettingsRequest : CloudSyncStatusRequest
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;
        [JsonPropertyName("revision")]
        public int? Revision { get; set; }
        [JsonPropertyName("settings")]
        public CloudSyncSettings? Settings { get; set; }
    }

    public class CloudSyncInfo
    {
        [JsonPropertyName("exists")]
        public bool Exists { get; set; }
        [JsonPropertyName("revision")]
        public int? Revision { get; set; }
        [JsonPropertyName("schemaVersion")]
        public int? SchemaVersion { get; set; }
        [JsonPropertyName("updatedAt")]
        public string? UpdatedAt { get; set; }
        [JsonPropertyName("lastDeviceId")]
        public int? LastDeviceId { get; set; }
    }

    public class CloudSyncResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        [JsonPropertyName("error")]
        public string? Error { get; set; }
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        [JsonPropertyName("serverRevision")]
        public int? ServerRevision { get; set; }
        [JsonPropertyName("sync")]
        public CloudSyncInfo? Sync { get; set; }
        [JsonPropertyName("settings")]
        public CloudSyncSettings? Settings { get; set; }
        [JsonPropertyName("deleted")]
        public bool? Deleted { get; set; }
    }
}
