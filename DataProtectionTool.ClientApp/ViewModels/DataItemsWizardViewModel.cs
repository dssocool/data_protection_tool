using Azure.Storage.Blobs;
using DataProtectionTool.ClientApp.Models;
using DataProtectionTool.ClientApp.Services;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataProtectionTool.ClientApp.ViewModels;

public sealed class DataItemsWizardViewModel : ObservableObject
{
    private const string SqlServerType = "Microsoft SQL Server";
    private const string FabricType = "Azure Fabric";
    private const string BlobType = "Azure Blob Storage";
    private const string EntraAuthType = "Entra";

    public ObservableCollection<DataItemRecord> Items { get; } = [];
    public Dictionary<string, ConnectionItem> ConnectionsByName { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void LoadItems()
    {
        Items.Clear();
        foreach (var item in DataItemConfigurationStore.Load())
        {
            if (item.SelectedConnections.Count == 0 && !string.IsNullOrWhiteSpace(item.Source))
            {
                item.SelectedConnections = item.Source
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(static part => !string.IsNullOrWhiteSpace(part))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (item.SelectedItemConnections.Count < item.SelectedItems.Count)
            {
                while (item.SelectedItemConnections.Count < item.SelectedItems.Count)
                {
                    item.SelectedItemConnections.Add(string.Empty);
                }
            }

            Items.Add(item);
        }
    }

    public void SaveItems()
    {
        DataItemConfigurationStore.Save(Items);
    }

    public void LoadConnections()
    {
        ConnectionsByName.Clear();
        foreach (var connection in ConnectionConfigurationStore.Load())
        {
            if (!string.IsNullOrWhiteSpace(connection.Name))
            {
                ConnectionsByName[connection.Name] = connection;
            }
        }
    }

    public string BuildUniqueName(string originalName)
    {
        var seed = string.IsNullOrWhiteSpace(originalName) ? "Data Item" : originalName.Trim();
        var candidate = $"{seed} Copy";
        var suffix = 2;
        while (Items.Any(item => item.ItemName.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{seed} Copy {suffix}";
            suffix++;
        }

        return candidate;
    }

    public string EnsureUniqueName(string baseName, DataItemRecord? currentItem)
    {
        var normalized = string.IsNullOrWhiteSpace(baseName) ? "Data Item" : baseName.Trim();
        if (!Items.Any(item => !ReferenceEquals(item, currentItem) && item.ItemName.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return normalized;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = $"{normalized} ({suffix})";
            if (!Items.Any(item => !ReferenceEquals(item, currentItem) && item.ItemName.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }

            suffix++;
        }
    }

    public async Task<List<FetchedItemOption>> FetchItemsForConnectionAsync(ConnectionItem connection, CancellationToken token)
    {
        var type = NormalizeConnectionType(connection.Type);
        return type switch
        {
            BlobType => await FetchBlobItemsAsync(connection, token),
            FabricType => await FetchSqlItemsAsync(connection, fabricMode: true, token),
            _ => await FetchSqlItemsAsync(connection, fabricMode: false, token)
        };
    }

    private static string NormalizeConnectionType(string? type)
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

    private static string NormalizeAuthType(string? authType)
    {
        return string.Equals(authType, EntraAuthType, StringComparison.OrdinalIgnoreCase) ? EntraAuthType : "SQL Server";
    }

    private static async Task<List<FetchedItemOption>> FetchSqlItemsAsync(ConnectionItem connection, bool fabricMode, CancellationToken token)
    {
        string connectionString;
        if (fabricMode)
        {
            if (string.IsNullOrWhiteSpace(connection.FabricConnectionString))
            {
                throw new InvalidOperationException("Fabric connection string is empty.");
            }

            connectionString = connection.FabricConnectionString;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(connection.SqlServerName) || string.IsNullOrWhiteSpace(connection.SqlDatabase))
            {
                throw new InvalidOperationException("SQL connection is missing server or database.");
            }

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = connection.SqlServerName,
                InitialCatalog = connection.SqlDatabase,
                Encrypt = true,
                TrustServerCertificate = true,
                ConnectTimeout = 12
            };

            if (NormalizeAuthType(connection.SqlAuthenticationType) == EntraAuthType)
            {
                builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
            }
            else
            {
                builder.UserID = connection.SqlUserName;
                builder.Password = connection.SqlPassword;
            }

            connectionString = builder.ConnectionString;
        }

        var result = new List<FetchedItemOption>();
        await using var sqlConnection = new SqlConnection(connectionString);
        await sqlConnection.OpenAsync(token);
        const string query = """
            SELECT TABLE_SCHEMA + '.' + TABLE_NAME AS ItemName, TABLE_TYPE AS ItemType
            FROM INFORMATION_SCHEMA.TABLES
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """;
        await using var command = new SqlCommand(query, sqlConnection);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var name = reader.GetString(0);
            var tableType = reader.GetString(1);
            var kindSuffix = tableType.Equals("VIEW", StringComparison.OrdinalIgnoreCase) ? "view" : "table";
            var kind = fabricMode ? $"fabric.{kindSuffix}" : $"sql.{kindSuffix}";
            result.Add(new FetchedItemOption
            {
                Name = name,
                Kind = kind,
                ConnectionName = connection.Name
            });
        }

        return result;
    }

    private static async Task<List<FetchedItemOption>> FetchBlobItemsAsync(ConnectionItem connection, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(connection.BlobStorageAccount)
            || string.IsNullOrWhiteSpace(connection.BlobContainer)
            || string.IsNullOrWhiteSpace(connection.BlobAccessKey))
        {
            throw new InvalidOperationException("Blob connection is missing account/container/access key.");
        }

        var result = new List<FetchedItemOption>();
        var connectionString = $"DefaultEndpointsProtocol=https;AccountName={connection.BlobStorageAccount};AccountKey={connection.BlobAccessKey};EndpointSuffix=core.windows.net";
        var containerClient = new BlobContainerClient(connectionString, connection.BlobContainer);

        var exists = await containerClient.ExistsAsync(token);
        if (!exists.Value)
        {
            throw new InvalidOperationException("Blob container does not exist.");
        }

        var take = 0;
        await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: token))
        {
            result.Add(new FetchedItemOption
            {
                Name = blob.Name,
                Kind = "blob.file",
                ConnectionName = connection.Name
            });

            take++;
            if (take >= 250)
            {
                break;
            }
        }

        return result;
    }
}
