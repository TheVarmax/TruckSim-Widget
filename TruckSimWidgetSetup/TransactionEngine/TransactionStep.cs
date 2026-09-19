using System.Text.Json.Serialization;

namespace TruckSimWidgetSetup.TransactionEngine;

public class TransactionStep
{
    [JsonPropertyName("stepIndex")]
    public int StepIndex { get; set; }

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = string.Empty;

    [JsonPropertyName("targetPath")]
    public string TargetPath { get; set; } = string.Empty;

    [JsonPropertyName("backupPath")]
    public string BackupPath { get; set; } = string.Empty;

    [JsonPropertyName("originalHash")]
    public string OriginalHash { get; set; } = string.Empty;

    [JsonPropertyName("newHash")]
    public string NewHash { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "STEP_PENDING";
}
