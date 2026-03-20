using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DataProtectionTool;

public static class ConnectionConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string FilePath => Path.Combine(AppStorage.ConfigDirectory, "connections.json");

    public static IReadOnlyList<ConnectionItem> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var json = File.ReadAllText(FilePath);
            var items = JsonSerializer.Deserialize<List<ConnectionItem>>(json, JsonOptions);
            return items ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<ConnectionItem> items)
    {
        AppStorage.EnsureConfigDirectoryExists();
        var json = JsonSerializer.Serialize(items, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
