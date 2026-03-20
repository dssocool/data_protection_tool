namespace DataProtectionTool.Models;

public sealed class FetchedItemOption
{
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string ConnectionName { get; init; } = string.Empty;

    public string Key => $"{ConnectionName}|{Kind}|{Name}";
    public string KindLabel => Kind switch
    {
        "sql.table" => "SQL TABLE",
        "sql.view" => "SQL VIEW",
        "fabric.table" => "FABRIC TABLE",
        "fabric.view" => "FABRIC VIEW",
        "blob.file" => "BLOB FILE",
        _ => "ITEM"
    };
    public string BadgeBackground => DataItemBadge.From(Kind, Name).Background;
    public string BadgeForeground => DataItemBadge.From(Kind, Name).Foreground;

    public static FetchedItemOption FromSaved(string name, string kind, string connectionName)
    {
        return new FetchedItemOption
        {
            Name = name,
            Kind = kind,
            ConnectionName = connectionName
        };
    }
}
