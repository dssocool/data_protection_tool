namespace DataProtectionTool;

public sealed class DataItemRecord
{
    public string ItemName { get; set; } = string.Empty;

    public string Classification { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Retention { get; set; } = string.Empty;
}
