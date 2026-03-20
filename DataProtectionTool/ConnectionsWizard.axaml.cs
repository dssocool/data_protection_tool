using Avalonia.Controls;
using Avalonia.Interactivity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;

namespace DataProtectionTool;

public partial class ConnectionsWizard : UserControl
{
    private enum ConnectionType
    {
        SqlServer,
        AzureFabric,
        AzureBlob
    }

    private enum SqlAuthType
    {
        Entra,
        SqlServer
    }

    private ConnectionType _selectedConnectionType = ConnectionType.SqlServer;
    private bool _isValidated;
    private bool _isInitializing;

    public ConnectionsWizard()
    {
        _isInitializing = true;
        InitializeComponent();
        _isInitializing = false;
        SqlAuthenticationTypeComboBox.SelectedIndex = 0;
        UpdateSqlCredentialState();
        UpdatePanels();
        SetStatus(string.Empty, isSuccess: true);
    }

    private void OnConnectionTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || !IsUiReady())
        {
            return;
        }

        _selectedConnectionType = ConnectionTypeList.SelectedIndex switch
        {
            1 => ConnectionType.AzureFabric,
            2 => ConnectionType.AzureBlob,
            _ => ConnectionType.SqlServer
        };

        UpdatePanels();
        MarkValidationDirty();
    }

    private void OnSqlAuthenticationTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || !IsUiReady())
        {
            return;
        }

        UpdateSqlCredentialState();
        MarkValidationDirty();
    }

    private void OnAnyInputChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isInitializing || !IsUiReady())
        {
            return;
        }

        MarkValidationDirty();
    }

    private async void OnValidateSaveClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            ValidateSaveButton.IsEnabled = false;

            if (_isValidated)
            {
                var profile = BuildCurrentProfile();
                ConnectionConfigurationStore.SaveProfile(profile);
                SetStatus("Connection profile saved.", isSuccess: true);
                return;
            }

            var result = await ValidateCurrentConnectionAsync();
            if (!result.IsSuccess)
            {
                SetStatus(result.Message, isSuccess: false);
                return;
            }

            _isValidated = true;
            ValidateSaveButton.Content = "Save";
            SetStatus("Validation succeeded. Click Save to persist this profile.", isSuccess: true);
        }
        catch (Exception ex)
        {
            SetStatus($"Operation failed: {ex.Message}", isSuccess: false);
        }
        finally
        {
            ValidateSaveButton.IsEnabled = true;
        }
    }

    private async Task<(bool IsSuccess, string Message)> ValidateCurrentConnectionAsync()
    {
        return _selectedConnectionType switch
        {
            ConnectionType.SqlServer => await ValidateSqlServerAsync(),
            ConnectionType.AzureFabric => await ValidateFabricAsync(),
            _ => await ValidateBlobAsync()
        };
    }

    private async Task<(bool IsSuccess, string Message)> ValidateSqlServerAsync()
    {
        try
        {
            var serverName = SqlServerNameTextBox.Text?.Trim() ?? string.Empty;
            var database = SqlDatabaseTextBox.Text?.Trim() ?? string.Empty;
            var authType = GetSelectedSqlAuthType();

            if (string.IsNullOrWhiteSpace(serverName) || string.IsNullOrWhiteSpace(database))
            {
                return (false, "Server Name and Database are required.");
            }

            var connectionBuilder = new SqlConnectionStringBuilder
            {
                DataSource = serverName,
                InitialCatalog = database,
                ConnectTimeout = 10,
                Encrypt = true,
                TrustServerCertificate = true
            };

            if (authType == SqlAuthType.Entra)
            {
                connectionBuilder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
            }
            else
            {
                var username = SqlUserNameTextBox.Text?.Trim() ?? string.Empty;
                var password = SqlPasswordTextBox.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    return (false, "User Name and Password are required for SQL Server authentication.");
                }

                connectionBuilder.Authentication = SqlAuthenticationMethod.SqlPassword;
                connectionBuilder.UserID = username;
                connectionBuilder.Password = password;
            }

            await using var connection = new SqlConnection(connectionBuilder.ConnectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand("SELECT 1", connection);
            _ = await command.ExecuteScalarAsync();
            return (true, "SQL Server connection validated.");
        }
        catch (Exception ex)
        {
            return (false, $"SQL Server validation failed: {ex.Message}");
        }
    }

    private async Task<(bool IsSuccess, string Message)> ValidateFabricAsync()
    {
        try
        {
            var connectionString = FabricConnectionStringTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return (false, "Connection String is required.");
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand("SELECT 1", connection);
            _ = await command.ExecuteScalarAsync();
            return (true, "Azure Fabric connection validated.");
        }
        catch (Exception ex)
        {
            return (false, $"Azure Fabric validation failed: {ex.Message}");
        }
    }

    private async Task<(bool IsSuccess, string Message)> ValidateBlobAsync()
    {
        try
        {
            var account = BlobStorageAccountTextBox.Text?.Trim() ?? string.Empty;
            var container = BlobContainerTextBox.Text?.Trim() ?? string.Empty;
            var accessKey = BlobAccessKeyTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(container) || string.IsNullOrWhiteSpace(accessKey))
            {
                return (false, "Storage Account, Container, and Access Key are required.");
            }

            var credential = new StorageSharedKeyCredential(account, accessKey);
            var containerUri = new Uri($"https://{account}.blob.core.windows.net/{container}");
            var containerClient = new BlobContainerClient(containerUri, credential);
            var existsResponse = await containerClient.ExistsAsync();
            if (!existsResponse.Value)
            {
                return (false, "Connected to storage account, but the specified container does not exist.");
            }

            return (true, "Azure Blob Storage connection validated.");
        }
        catch (Exception ex)
        {
            return (false, $"Azure Blob validation failed: {ex.Message}");
        }
    }

    private ConnectionProfile BuildCurrentProfile()
    {
        var profile = new ConnectionProfile
        {
            ConnectionType = _selectedConnectionType switch
            {
                ConnectionType.AzureFabric => "Azure Fabric",
                ConnectionType.AzureBlob => "Azure Blob Storage",
                _ => "Microsoft SQL Server"
            },
            SavedAtUtc = DateTime.UtcNow
        };

        if (_selectedConnectionType == ConnectionType.SqlServer)
        {
            profile.SqlServerName = SqlServerNameTextBox.Text?.Trim() ?? string.Empty;
            profile.SqlAuthenticationType = GetSelectedSqlAuthType() == SqlAuthType.Entra ? "Entra" : "SQL Server";
            profile.SqlUserName = SqlUserNameTextBox.Text?.Trim() ?? string.Empty;
            profile.SqlPassword = SqlPasswordTextBox.Text ?? string.Empty;
            profile.SqlDatabase = SqlDatabaseTextBox.Text?.Trim() ?? string.Empty;
        }
        else if (_selectedConnectionType == ConnectionType.AzureFabric)
        {
            profile.FabricConnectionString = FabricConnectionStringTextBox.Text?.Trim() ?? string.Empty;
        }
        else
        {
            profile.BlobStorageAccount = BlobStorageAccountTextBox.Text?.Trim() ?? string.Empty;
            profile.BlobContainer = BlobContainerTextBox.Text?.Trim() ?? string.Empty;
            profile.BlobAccessKey = BlobAccessKeyTextBox.Text?.Trim() ?? string.Empty;
        }

        return profile;
    }

    private SqlAuthType GetSelectedSqlAuthType()
    {
        return SqlAuthenticationTypeComboBox.SelectedIndex == 1
            ? SqlAuthType.SqlServer
            : SqlAuthType.Entra;
    }

    private void UpdatePanels()
    {
        SqlServerPanel.IsVisible = _selectedConnectionType == ConnectionType.SqlServer;
        FabricPanel.IsVisible = _selectedConnectionType == ConnectionType.AzureFabric;
        BlobPanel.IsVisible = _selectedConnectionType == ConnectionType.AzureBlob;
    }

    private void UpdateSqlCredentialState()
    {
        var enableSqlCredentials = GetSelectedSqlAuthType() == SqlAuthType.SqlServer;
        SqlUserNameTextBox.IsEnabled = enableSqlCredentials;
        SqlPasswordTextBox.IsEnabled = enableSqlCredentials;
    }

    private void MarkValidationDirty()
    {
        if (!_isValidated)
        {
            return;
        }

        _isValidated = false;
        ValidateSaveButton.Content = "Validate";
        SetStatus("Connection details changed. Re-validate before saving.", isSuccess: false);
    }

    private void SetStatus(string message, bool isSuccess)
    {
        ConnectionStatusText.Text = message;
        ConnectionStatusText.Foreground = isSuccess ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2D6A4F")) : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#B42318"));
        if (string.IsNullOrWhiteSpace(message))
        {
            ConnectionStatusText.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4C6388"));
        }
    }

    private bool IsUiReady()
    {
        return ConnectionTypeList is not null
            && SqlAuthenticationTypeComboBox is not null
            && SqlServerPanel is not null
            && FabricPanel is not null
            && BlobPanel is not null
            && SqlUserNameTextBox is not null
            && SqlPasswordTextBox is not null
            && ValidateSaveButton is not null
            && ConnectionStatusText is not null;
    }
}
