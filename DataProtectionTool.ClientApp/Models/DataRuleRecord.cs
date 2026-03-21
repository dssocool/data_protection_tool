namespace DataProtectionTool.ClientApp.Models;

public sealed class DataRuleRecord
{
    public string RuleName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Severity { get; set; } = "Medium";

    public string AppliesTo { get; set; } = string.Empty;
}
