using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DataProtectionTool;

public static class DataItemConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string FilePath => Path.Combine(AppStorage.ConfigDirectory, "data-items.json");

    public static IReadOnlyList<DataItemRecord> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var json = File.ReadAllText(FilePath);
            var items = JsonSerializer.Deserialize<List<DataItemRecord>>(json, JsonOptions);
            return items ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<DataItemRecord> items)
    {
        AppStorage.EnsureConfigDirectoryExists();
        var json = JsonSerializer.Serialize(items, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
