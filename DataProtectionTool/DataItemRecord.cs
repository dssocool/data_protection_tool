using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DataProtectionTool;

public sealed class DataItemRecord
{
    public string ItemName { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public List<string> SelectedConnections { get; set; } = [];

    public List<string> SelectedItems { get; set; } = [];

    public List<string> SelectedItemKinds { get; set; } = [];

    public List<string> SelectedItemConnections { get; set; } = [];

    [JsonIgnore]
    public string ConnectionsSummary => string.Join(", ", SelectedConnections.Where(static item => !string.IsNullOrWhiteSpace(item)));

    [JsonIgnore]
    public List<DataItemBadge> ConnectionBadges =>
    [
        .. SelectedConnections
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => new DataItemBadge
            {
                Text = name,
                Background = "#EEF3FB",
                Foreground = "#244268"
            })
    ];

    [JsonIgnore]
    public List<DataItemBadge> ItemBadges
    {
        get
        {
            var result = new List<DataItemBadge>();
            for (var index = 0; index < SelectedItems.Count; index++)
            {
                var itemName = SelectedItems[index];
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    continue;
                }

                var kind = index < SelectedItemKinds.Count ? SelectedItemKinds[index] : string.Empty;
                result.Add(DataItemBadge.From(kind, itemName));
            }

            return result;
        }
    }
}

public sealed class DataItemBadge
{
    public string Text { get; init; } = string.Empty;
    public string Background { get; init; } = "#E8EEF8";
    public string Foreground { get; init; } = "#21314A";

    public static DataItemBadge From(string kind, string text)
    {
        var normalizedKind = (kind ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedKind switch
        {
            "sql.table" => new DataItemBadge { Text = text, Background = "#DCEBFF", Foreground = "#1A4A88" },
            "sql.view" => new DataItemBadge { Text = text, Background = "#E5F4FF", Foreground = "#0F5D7A" },
            "fabric.table" => new DataItemBadge { Text = text, Background = "#E8E2FF", Foreground = "#5533A5" },
            "fabric.view" => new DataItemBadge { Text = text, Background = "#F0E9FF", Foreground = "#6541B5" },
            "blob.file" => new DataItemBadge { Text = text, Background = "#E6F8EA", Foreground = "#216A3A" },
            _ => new DataItemBadge { Text = text, Background = "#EDF1F8", Foreground = "#34445F" }
        };
    }
}
