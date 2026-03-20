using System;
using System.IO;

namespace DataProtectionTool;

public static class AppStorage
{
    private const string AppDirectoryName = "DataProtectionTool";

    public static string ConfigDirectory => Path.Combine(GetBaseConfigPath(), AppDirectoryName);

    public static void EnsureConfigDirectoryExists()
    {
        Directory.CreateDirectory(ConfigDirectory);
    }

    private static string GetBaseConfigPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appDataPath))
        {
            return appDataPath;
        }

        var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppDataPath))
        {
            return localAppDataPath;
        }

        return AppContext.BaseDirectory;
    }
}
