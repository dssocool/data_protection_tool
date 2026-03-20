using System;

namespace DataProtectionTool;

public class ConnectionProfile
{
    public string ConnectionType { get; set; } = string.Empty;
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

    public string SqlServerName { get; set; } = string.Empty;
    public string SqlAuthenticationType { get; set; } = string.Empty;
    public string SqlUserName { get; set; } = string.Empty;
    public string SqlPassword { get; set; } = string.Empty;
    public string SqlDatabase { get; set; } = string.Empty;

    public string FabricConnectionString { get; set; } = string.Empty;

    public string BlobStorageAccount { get; set; } = string.Empty;
    public string BlobContainer { get; set; } = string.Empty;
    public string BlobAccessKey { get; set; } = string.Empty;
}
