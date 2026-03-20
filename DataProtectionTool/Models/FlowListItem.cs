namespace DataProtectionTool.Models;

public sealed class FlowListItem
{
    public string FlowName { get; set; } = string.Empty;

    public string Domain { get; set; } = "-";

    public string Source { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public string Status { get; set; } = "Saved";

    public string Action { get; set; } = "Edit";

    public string DataItems { get; set; } = string.Empty;

    public string DataRules { get; set; } = string.Empty;
}
