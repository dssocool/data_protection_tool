namespace DataProtectionTool;

public sealed class FlowListItem
{
    public string FlowName { get; init; } = string.Empty;

    public string Domain { get; init; } = "-";

    public string Source { get; init; } = string.Empty;

    public string Destination { get; init; } = string.Empty;

    public string Status { get; init; } = "Saved";

    public string Action { get; init; } = "Edit";

    public string DataItems { get; init; } = string.Empty;

    public string DataRules { get; init; } = string.Empty;
}
