using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DataProtectionTool;

public static class DataItemSelectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string FilePath => Path.Combine(AppStorage.ConfigDirectory, "data-item-selections.json");

    public static IReadOnlyList<DataItemSelectionRecord> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var json = File.ReadAllText(FilePath);
            var records = JsonSerializer.Deserialize<List<DataItemSelectionRecord>>(json, JsonOptions);
            return records ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void SaveBatch(IEnumerable<DataItemSelectionRecord> records)
    {
        var combined = new List<DataItemSelectionRecord>(Load());
        combined.AddRange(records);

        AppStorage.EnsureConfigDirectoryExists();
        var json = JsonSerializer.Serialize(combined, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
