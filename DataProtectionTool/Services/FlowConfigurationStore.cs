using DataProtectionTool.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DataProtectionTool.Services;

public static class FlowConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string FilePath => Path.Combine(AppStorage.ConfigDirectory, "flows.json");

    public static IReadOnlyList<FlowListItem> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var json = File.ReadAllText(FilePath);
            var flows = JsonSerializer.Deserialize<List<FlowListItem>>(json, JsonOptions);
            return flows ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<FlowListItem> flows)
    {
        AppStorage.EnsureConfigDirectoryExists();
        var json = JsonSerializer.Serialize(flows, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
