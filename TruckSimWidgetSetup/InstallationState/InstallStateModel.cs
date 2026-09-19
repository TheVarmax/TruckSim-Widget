using System.Text.Json;
using System.Text.Json.Serialization;
using TruckSimWidgetSetup.Common;
using TruckSimWidgetSetup.Diagnostics;

namespace TruckSimWidgetSetup.InstallationState;

public class GameInstallState
{
    [JsonPropertyName("gameId")]
    public string GameId { get; set; } = string.Empty;

    [JsonPropertyName("configured")]
    public bool Configured { get; set; }

    [JsonPropertyName("gamePath")]
    public string GamePath { get; set; } = string.Empty;

    [JsonPropertyName("pluginPath")]
    public string PluginPath { get; set; } = string.Empty;

    [JsonPropertyName("ownershipStatus")]
    public string OwnershipStatus { get; set; } = "None";

    [JsonPropertyName("installedPluginHash")]
    public string InstalledPluginHash { get; set; } = string.Empty;

    [JsonPropertyName("backupPath")]
    public string BackupPath { get; set; } = string.Empty;
}

public class InstalledFileRecord
{
    [JsonPropertyName("path")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;
}

public class InstallStateModel
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 3;

    [JsonPropertyName("installerVersion")]
    public string InstallerVersion { get; set; } = string.Empty;

    [JsonPropertyName("lastUpdated")]
    public string LastUpdated { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

    [JsonPropertyName("installPath")]
    public string InstallPath { get; set; } = string.Empty;

    [JsonPropertyName("games")]
    public Dictionary<string, GameInstallState> Games { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Explicit list of files owned by this installation.
    /// Preserves deterministic ownership across subsequent updates and uninstalls.
    /// </summary>
    [JsonPropertyName("installedFiles")]
    public List<InstalledFileRecord> InstalledFiles { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static InstallStateModel? FromJson(string json) =>
        JsonSerializer.Deserialize<InstallStateModel>(json, JsonOptions);

    public static InstallStateModel? LoadFromFile(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            var model = FromJson(json);
            if (model != null && model.SchemaVersion == 3)
            {
                return model;
            }

            // If legacy v2 format, migrate to v3
            return MigrateLegacyV2(json);
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Failed to load install state from {path}: {ex.Message}");
            return null;
        }
    }

    private static InstallStateModel? MigrateLegacyV2(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var model = new InstallStateModel
            {
                SchemaVersion = 3,
                InstallerVersion = root.TryGetProperty("installed_version", out var v) ? v.GetString() ?? "" : "",
                LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                InstallPath = Constants.GetDefaultAppDir()
            };

            string ets2Path = root.TryGetProperty("ets2_path", out var ep) ? ep.GetString() ?? "" : "";
            string ets2Ownership = root.TryGetProperty("ets2_ownership", out var eo) ? eo.GetString() ?? "None" : "None";
            string ets2Hash = root.TryGetProperty("ets2_hash", out var eh) ? eh.GetString() ?? "" : "";
            string ets2Backup = root.TryGetProperty("ets2_backup", out var eb) ? eb.GetString() ?? "" : "";
            bool ets2Enabled = root.TryGetProperty("ets2_enabled", out var ee) && ee.GetString() == "true";

            model.Games[Constants.GameIdEts2] = new GameInstallState
            {
                GameId = Constants.GameIdEts2,
                Configured = ets2Enabled,
                GamePath = ets2Path,
                PluginPath = string.IsNullOrEmpty(ets2Path) ? "" : Path.Combine(ets2Path, Constants.GameRelPluginDir, Constants.PluginFileName),
                OwnershipStatus = ets2Ownership,
                InstalledPluginHash = ets2Hash,
                BackupPath = ets2Backup
            };

            string atsPath = root.TryGetProperty("ats_path", out var ap) ? ap.GetString() ?? "" : "";
            string atsOwnership = root.TryGetProperty("ats_ownership", out var ao) ? ao.GetString() ?? "None" : "None";
            string atsHash = root.TryGetProperty("ats_hash", out var ah) ? ah.GetString() ?? "" : "";
            string atsBackup = root.TryGetProperty("ats_backup", out var ab) ? ab.GetString() ?? "" : "";
            bool atsEnabled = root.TryGetProperty("ats_enabled", out var ae) && ae.GetString() == "true";

            model.Games[Constants.GameIdAts] = new GameInstallState
            {
                GameId = Constants.GameIdAts,
                Configured = atsEnabled,
                GamePath = atsPath,
                PluginPath = string.IsNullOrEmpty(atsPath) ? "" : Path.Combine(atsPath, Constants.GameRelPluginDir, Constants.PluginFileName),
                OwnershipStatus = atsOwnership,
                InstalledPluginHash = atsHash,
                BackupPath = atsBackup
            };

            InstallerLogger.LogInfo("Successfully migrated legacy v2 install state to canonical v3 model.");
            return model;
        }
        catch (Exception ex)
        {
            InstallerLogger.LogWarn($"Failed to migrate legacy v2 install state: {ex.Message}");
            return null;
        }
    }
}
