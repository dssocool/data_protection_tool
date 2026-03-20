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

    public static IReadOnlyList<ConnectionProfile> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var json = File.ReadAllText(FilePath);
            var profiles = JsonSerializer.Deserialize<List<ConnectionProfile>>(json, JsonOptions);
            return profiles ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void SaveProfile(ConnectionProfile profile)
    {
        var existing = new List<ConnectionProfile>(Load())
        {
            profile
        };

        SaveAll(existing);
    }

    private static void SaveAll(IEnumerable<ConnectionProfile> profiles)
    {
        AppStorage.EnsureConfigDirectoryExists();
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
