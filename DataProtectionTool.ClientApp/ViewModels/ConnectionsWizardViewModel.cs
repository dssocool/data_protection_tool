using Azure;
using Azure.Storage.Blobs;
using DataProtectionTool.ClientApp.Models;
using DataProtectionTool.ClientApp.Services;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataProtectionTool.ClientApp.ViewModels;

public sealed class ConnectionsWizardViewModel : ObservableObject
{
    public const string SqlServerType = "Microsoft SQL Server";
    public const string FabricType = "Azure Fabric";
    public const string BlobType = "Azure Blob Storage";
    public const string EntraAuthType = "Entra";
    public const string SqlServerAuthType = "SQL Server";

    public ObservableCollection<ConnectionItem> Items { get; } = [];

    public void LoadItems()
    {
        Items.Clear();
        foreach (var item in ConnectionConfigurationStore.Load())
        {
            Items.Add(item);
        }
    }

    public void SaveItems()
    {
        ConnectionConfigurationStore.Save(Items);
    }

    public async Task<(bool IsValid, string Message)> ValidateConnectionAsync(ConnectionItem draft, string selectedConnectionType, string selectedAuthenticationType)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var token = cts.Token;

            return selectedConnectionType switch
            {
                SqlServerType => await ValidateSqlConnectionAsync(draft, selectedAuthenticationType, token),
                FabricType => await ValidateFabricConnectionAsync(draft, token),
                BlobType => await ValidateBlobConnectionAsync(draft, token),
                _ => (false, "Unknown connection type.")
            };
        }
        catch (OperationCanceledException)
        {
            return (false, "Validation timed out.");
        }
        catch (RequestFailedException ex)
        {
            return (false, $"Validation failed: {ex.Message}");
        }
        catch (SqlException ex)
        {
            return (false, $"Validation failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Validation failed: {ex.Message}");
        }
    }

    public string NormalizeType(string? type)
    {
        if (string.Equals(type, FabricType, StringComparison.OrdinalIgnoreCase))
        {
            return FabricType;
        }

        if (string.Equals(type, BlobType, StringComparison.OrdinalIgnoreCase))
        {
            return BlobType;
        }

        return SqlServerType;
    }

    public string NormalizeAuthType(string? authType)
    {
        if (string.Equals(authType, EntraAuthType, StringComparison.OrdinalIgnoreCase))
        {
            return EntraAuthType;
        }

        return SqlServerAuthType;
    }

    public string BuildUniqueName(string originalName)
    {
        var seed = string.IsNullOrWhiteSpace(originalName) ? "Connection" : originalName.Trim();
        var candidate = $"{seed} Copy";
        var suffix = 2;
        while (Items.Any(item => item.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{seed} Copy {suffix}";
            suffix++;
        }

        return candidate;
    }

    public string EnsureUniqueName(string baseName, ConnectionItem? currentItem)
    {
        var normalized = string.IsNullOrWhiteSpace(baseName) ? "Connection" : baseName.Trim();
        if (!Items.Any(item => !ReferenceEquals(item, currentItem) && item.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return normalized;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = $"{normalized} ({suffix})";
            if (!Items.Any(item => !ReferenceEquals(item, currentItem) && item.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }

            suffix++;
        }
    }

    public string BuildEndpoint(ConnectionItem item, string selectedConnectionType)
    {
        return selectedConnectionType switch
        {
            SqlServerType => item.SqlServerName,
            FabricType => item.FabricConnectionString,
            BlobType => item.BlobStorageAccount,
            _ => string.Empty
        };
    }

    private async Task<(bool IsValid, string Message)> ValidateSqlConnectionAsync(ConnectionItem draft, string selectedAuthenticationType, CancellationToken cancellationToken)
    {
        var serverName = draft.SqlServerName?.Trim() ?? string.Empty;
        var database = draft.SqlDatabase?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(serverName) || string.IsNullOrWhiteSpace(database))
        {
            return (false, "Server Name and Database are required for SQL Server.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = serverName,
            InitialCatalog = database,
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 12
        };

        if (selectedAuthenticationType == EntraAuthType)
        {
            builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
        }
        else
        {
            var userName = draft.SqlUserName?.Trim() ?? string.Empty;
            var password = draft.SqlPassword?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "User Name and Password are required for SQL Server authentication.");
            }

            builder.UserID = userName;
            builder.Password = password;
        }

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return (true, "Validation successful. Click Save to persist.");
    }

    private static async Task<(bool IsValid, string Message)> ValidateFabricConnectionAsync(ConnectionItem draft, CancellationToken cancellationToken)
    {
        var connectionString = draft.FabricConnectionString?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return (false, "Connection String is required for Azure Fabric.");
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return (true, "Validation successful. Click Save to persist.");
    }

    private static async Task<(bool IsValid, string Message)> ValidateBlobConnectionAsync(ConnectionItem draft, CancellationToken cancellationToken)
    {
        var storageAccount = draft.BlobStorageAccount?.Trim() ?? string.Empty;
        var container = draft.BlobContainer?.Trim() ?? string.Empty;
        var accessKey = draft.BlobAccessKey?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(storageAccount)
            || string.IsNullOrWhiteSpace(container)
            || string.IsNullOrWhiteSpace(accessKey))
        {
            return (false, "Storage Account, Container, and Access Key are required for Azure Blob.");
        }

        var connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccount};AccountKey={accessKey};EndpointSuffix=core.windows.net";
        var containerClient = new BlobContainerClient(connectionString, container);
        var exists = await containerClient.ExistsAsync(cancellationToken);
        if (!exists.Value)
        {
            return (false, "Validation failed: container does not exist.");
        }

        return (true, "Validation successful. Click Save to persist.");
    }
}
