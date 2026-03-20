using DataProtectionTool.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DataProtectionTool.Services;

public static class DataRuleConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string FilePath => Path.Combine(AppStorage.ConfigDirectory, "data-rules.json");

    public static IReadOnlyList<DataRuleRecord> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var json = File.ReadAllText(FilePath);
            var items = JsonSerializer.Deserialize<List<DataRuleRecord>>(json, JsonOptions);
            return items ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<DataRuleRecord> items)
    {
        AppStorage.EnsureConfigDirectoryExists();
        var json = JsonSerializer.Serialize(items, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
