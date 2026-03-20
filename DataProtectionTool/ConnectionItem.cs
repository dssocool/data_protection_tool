namespace DataProtectionTool;

public sealed class ConnectionItem
{
    public string Name { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string Type { get; set; } = "Custom";

    public string Notes { get; set; } = string.Empty;
}
