namespace DataProtectionTool;

public sealed class FlowDetailsInput
{
    public string FlowName { get; init; } = string.Empty;

    public string SourceConnection { get; init; } = string.Empty;

    public string DataItems { get; init; } = string.Empty;

    public string DataRules { get; init; } = string.Empty;

    public string Destination { get; init; } = string.Empty;
}
