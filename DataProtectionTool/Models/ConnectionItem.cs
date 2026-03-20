namespace DataProtectionTool.Models;

public sealed class ConnectionItem
{
    public string Name { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string Type { get; set; } = "Microsoft SQL Server";

    public string Notes { get; set; } = string.Empty;

    public string SqlServerName { get; set; } = string.Empty;

    public string SqlAuthenticationType { get; set; } = "SQL Server";

    public string SqlUserName { get; set; } = string.Empty;

    public string SqlPassword { get; set; } = string.Empty;

    public string SqlDatabase { get; set; } = string.Empty;

    public string FabricConnectionString { get; set; } = string.Empty;

    public string BlobStorageAccount { get; set; } = string.Empty;

    public string BlobContainer { get; set; } = string.Empty;

    public string BlobAccessKey { get; set; } = string.Empty;
}
