using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ETSOverlay
{
    public class CloudSyncStatusRequest
    {
        [JsonPropertyName("deviceToken")]
        public string DeviceToken { get; set; } = string.Empty;
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
        public Dictionary<string, object>? Settings { get; set; }
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
        public Dictionary<string, JsonElement>? Settings { get; set; }
        [JsonPropertyName("deleted")]
        public bool? Deleted { get; set; }
    }
}
