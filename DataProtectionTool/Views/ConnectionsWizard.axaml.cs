using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Azure;
using Azure.Storage.Blobs;
using DataProtectionTool.Models;
using DataProtectionTool.Services;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataProtectionTool.Views;

public partial class ConnectionsWizard : UserControl
{
    private const string SqlServerType = "Microsoft SQL Server";
    private const string FabricType = "Azure Fabric";
    private const string BlobType = "Azure Blob Storage";
    private const string EntraAuthType = "Entra";
    private const string SqlServerAuthType = "SQL Server";

    private readonly ObservableCollection<ConnectionItem> _items = [];
    private ConnectionItem? _selectedItem;
    private ConnectionItem? _editingItem;
    private bool _isEditMode;
    private bool _pendingDeleteConfirmation;
    private bool _isValidated;
    private bool _isPopulatingFields;
    private string _selectedConnectionType = SqlServerType;

    public ConnectionsWizard()
    {
        InitializeComponent();
        ItemsListBox.ItemsSource = _items;
        SqlAuthenticationComboBox.ItemsSource = new[] { EntraAuthType, SqlServerAuthType };
        SqlAuthenticationComboBox.SelectedItem = SqlServerAuthType;
        SelectConnectionType(SqlServerType, userInitiated: false);
        LoadItems();
        RefreshUi();
    }

    private void OnAddNewClicked(object? sender, RoutedEventArgs e)
    {
        ItemsListBox.SelectedItem = null;
        _selectedItem = null;
        _editingItem = null;
        _isEditMode = true;
        _pendingDeleteConfirmation = false;
        _isValidated = false;
        DeleteButton.Content = "Delete";
        ClearFields();
        SelectConnectionType(SqlServerType, userInitiated: false);
        UpdateFieldEditability();
        NameTextBox.Focus();
        RefreshUi();
    }

    private void OnItemRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control row || row.DataContext is not ConnectionItem item)
        {
            return;
        }

        _isEditMode = false;
        _pendingDeleteConfirmation = false;
        _isValidated = false;
        DeleteButton.Content = "Delete";

        if (ReferenceEquals(_selectedItem, item))
        {
            ItemsListBox.SelectedItem = null;
            _selectedItem = null;
            _editingItem = null;
        }
        else
        {
            _selectedItem = item;
            _editingItem = item;
            ItemsListBox.SelectedItem = item;
        }

        PopulateFieldsFromSelected();
        RefreshUi();
        e.Handled = true;
    }

    private void OnEditClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedItem is null)
        {
            return;
        }

        _isEditMode = true;
        _editingItem = _selectedItem;
        _pendingDeleteConfirmation = false;
        _isValidated = false;
        DeleteButton.Content = "Delete";
        UpdateFieldEditability();
        NameTextBox.Focus();
        RefreshUi();
    }

    private void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedItem is null)
        {
            return;
        }

        var copy = new ConnectionItem
        {
            Name = BuildUniqueName(_selectedItem.Name),
            Type = _selectedItem.Type,
            SqlServerName = _selectedItem.SqlServerName,
            SqlAuthenticationType = _selectedItem.SqlAuthenticationType,
            SqlUserName = _selectedItem.SqlUserName,
            SqlPassword = _selectedItem.SqlPassword,
            SqlDatabase = _selectedItem.SqlDatabase,
            FabricConnectionString = _selectedItem.FabricConnectionString,
            BlobStorageAccount = _selectedItem.BlobStorageAccount,
            BlobContainer = _selectedItem.BlobContainer,
            BlobAccessKey = _selectedItem.BlobAccessKey,
            Endpoint = _selectedItem.Endpoint,
            Notes = _selectedItem.Notes
        };

        _items.Add(copy);
        SaveItems();
        ItemsListBox.SelectedItem = copy;
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedItem is null)
        {
            return;
        }

        if (!_pendingDeleteConfirmation)
        {
            _pendingDeleteConfirmation = true;
            DeleteButton.Content = "Confirm Delete";
            StatusText.Text = "Click Confirm Delete to remove this connection.";
            return;
        }

        _items.Remove(_selectedItem);
        SaveItems();
        _pendingDeleteConfirmation = false;
        DeleteButton.Content = "Delete";
        _selectedItem = null;
        _editingItem = null;
        ItemsListBox.SelectedItem = null;
        PopulateFieldsFromSelected();
        RefreshUi();
    }

    private void OnCancelEditClicked(object? sender, RoutedEventArgs e)
    {
        _isEditMode = false;
        _pendingDeleteConfirmation = false;
        _isValidated = false;
        DeleteButton.Content = "Delete";
        PopulateFieldsFromSelected();
        RefreshUi();
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText.Text = "Name is required.";
            return;
        }

        if (!_isValidated)
        {
            SaveButton.IsEnabled = false;
            StatusText.Text = "Validating connection...";
            var isValid = await ValidateConnectionAsync();
            SaveButton.IsEnabled = true;
            if (!isValid)
            {
                SaveButton.Content = "Validate";
                return;
            }

            _isValidated = true;
            SaveButton.Content = "Save";
            StatusText.Text = "Validation successful. Click Save to persist.";
            return;
        }

        if (_editingItem is null)
        {
            var newItem = new ConnectionItem
            {
                Name = EnsureUniqueName(name, null)
            };
            ApplyFieldsToItem(newItem);
            _items.Add(newItem);
            SaveItems();
            ItemsListBox.SelectedItem = newItem;
            StatusText.Text = "Connection created.";
        }
        else
        {
            _editingItem.Name = EnsureUniqueName(name, _editingItem);
            ApplyFieldsToItem(_editingItem);
            SaveItems();
            ItemsListBox.ItemsSource = null;
            ItemsListBox.ItemsSource = _items;
            ItemsListBox.SelectedItem = _editingItem;
            StatusText.Text = "Connection updated.";
        }

        _isEditMode = false;
        _isValidated = false;
        _pendingDeleteConfirmation = false;
        DeleteButton.Content = "Delete";
        PopulateFieldsFromSelected();
        RefreshUi();
    }

    private void OnSqlServerTabClicked(object? sender, RoutedEventArgs e) => SelectConnectionType(SqlServerType, userInitiated: true);

    private void OnFabricTabClicked(object? sender, RoutedEventArgs e) => SelectConnectionType(FabricType, userInitiated: true);

    private void OnBlobTabClicked(object? sender, RoutedEventArgs e) => SelectConnectionType(BlobType, userInitiated: true);

    private void OnAnyInputChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isPopulatingFields || !_isEditMode)
        {
            return;
        }

        ResetValidationState();
    }

    private void OnSqlAuthenticationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isPopulatingFields)
        {
            return;
        }

        UpdateSqlCredentialEditability();
        if (_isEditMode)
        {
            ResetValidationState();
        }
    }

    private void LoadItems()
    {
        _items.Clear();
        foreach (var item in ConnectionConfigurationStore.Load())
        {
            _items.Add(item);
        }

        ItemsListBox.SelectedItem = null;
        _selectedItem = null;
        _editingItem = null;
        PopulateFieldsFromSelected();
    }

    private void SaveItems()
    {
        ConnectionConfigurationStore.Save(_items);
    }

    private void PopulateFieldsFromSelected()
    {
        _isPopulatingFields = true;
        try
        {
            if (_selectedItem is null)
            {
                ClearFields();
                return;
            }

            NameTextBox.Text = _selectedItem.Name;
            SelectConnectionType(NormalizeType(_selectedItem.Type), userInitiated: false);

            SqlServerNameTextBox.Text = _selectedItem.SqlServerName;
            SqlDatabaseTextBox.Text = _selectedItem.SqlDatabase;
            SqlUserNameTextBox.Text = _selectedItem.SqlUserName;
            SqlPasswordBox.Text = _selectedItem.SqlPassword;
            SqlAuthenticationComboBox.SelectedItem = NormalizeAuthType(_selectedItem.SqlAuthenticationType);

            FabricConnectionStringTextBox.Text = _selectedItem.FabricConnectionString;
            BlobStorageAccountTextBox.Text = _selectedItem.BlobStorageAccount;
            BlobContainerTextBox.Text = _selectedItem.BlobContainer;
            BlobAccessKeyBox.Text = _selectedItem.BlobAccessKey;
        }
        finally
        {
            _isPopulatingFields = false;
        }

        UpdateFieldEditability();
    }

    private void ClearFields()
    {
        _isPopulatingFields = true;
        try
        {
            NameTextBox.Text = string.Empty;
            SqlServerNameTextBox.Text = string.Empty;
            SqlDatabaseTextBox.Text = string.Empty;
            SqlUserNameTextBox.Text = string.Empty;
            SqlPasswordBox.Text = string.Empty;
            SqlAuthenticationComboBox.SelectedItem = SqlServerAuthType;
            FabricConnectionStringTextBox.Text = string.Empty;
            BlobStorageAccountTextBox.Text = string.Empty;
            BlobContainerTextBox.Text = string.Empty;
            BlobAccessKeyBox.Text = string.Empty;
        }
        finally
        {
            _isPopulatingFields = false;
        }

        UpdateFieldEditability();
    }

    private void RefreshUi()
    {
        var hasItems = _items.Count > 0;
        EmptyStateText.IsVisible = !hasItems;
        ItemsListBox.IsVisible = hasItems;

        var hasSelection = _selectedItem is not null;
        DetailsPanel.IsVisible = hasSelection || _isEditMode;
        EditButton.IsEnabled = hasSelection && !_isEditMode;
        CopyButton.IsEnabled = hasSelection && !_isEditMode;
        DeleteButton.IsEnabled = hasSelection && !_isEditMode;
        SaveButton.IsVisible = _isEditMode;
        SaveButton.Content = _isValidated ? "Save" : "Validate";
        CancelEditButton.IsVisible = _isEditMode;

        SqlServerTabButton.IsEnabled = _isEditMode;
        FabricTabButton.IsEnabled = _isEditMode;
        BlobTabButton.IsEnabled = _isEditMode;

        UpdateTabSelection();
        UpdateTypeFieldVisibility();
        UpdateFieldEditability();

        if (!_isEditMode && hasSelection)
        {
            StatusText.Text = "Select Edit to modify this connection.";
        }
        else if (!_isEditMode)
        {
            StatusText.Text = "Select a connection or click Add New.";
        }
    }

    private void SelectConnectionType(string type, bool userInitiated)
    {
        var normalizedType = NormalizeType(type);
        if (_selectedConnectionType == normalizedType)
        {
            return;
        }

        _selectedConnectionType = normalizedType;
        UpdateTabSelection();
        UpdateTypeFieldVisibility();
        UpdateFieldEditability();

        if (userInitiated && _isEditMode)
        {
            ResetValidationState();
        }
    }

    private void UpdateTabSelection()
    {
        SetClass(SqlServerTabButton, "selected", _selectedConnectionType == SqlServerType);
        SetClass(FabricTabButton, "selected", _selectedConnectionType == FabricType);
        SetClass(BlobTabButton, "selected", _selectedConnectionType == BlobType);
    }

    private void UpdateTypeFieldVisibility()
    {
        SqlFieldsPanel.IsVisible = _selectedConnectionType == SqlServerType;
        FabricFieldsPanel.IsVisible = _selectedConnectionType == FabricType;
        BlobFieldsPanel.IsVisible = _selectedConnectionType == BlobType;
    }

    private void UpdateFieldEditability()
    {
        var canEdit = _isEditMode;

        NameTextBox.IsReadOnly = !canEdit;
        SqlServerNameTextBox.IsReadOnly = !canEdit;
        SqlDatabaseTextBox.IsReadOnly = !canEdit;
        SqlUserNameTextBox.IsReadOnly = !canEdit;
        FabricConnectionStringTextBox.IsReadOnly = !canEdit;
        BlobStorageAccountTextBox.IsReadOnly = !canEdit;
        BlobContainerTextBox.IsReadOnly = !canEdit;
        SqlAuthenticationComboBox.IsEnabled = canEdit && _selectedConnectionType == SqlServerType;
        BlobAccessKeyBox.IsReadOnly = !(canEdit && _selectedConnectionType == BlobType);

        UpdateSqlCredentialEditability();
    }

    private void UpdateSqlCredentialEditability()
    {
        var isSqlType = _selectedConnectionType == SqlServerType;
        var canEditCredentials = _isEditMode && isSqlType && SelectedAuthenticationType == SqlServerAuthType;

        SqlUserNameTextBox.IsReadOnly = !canEditCredentials;
        SqlPasswordBox.IsReadOnly = !canEditCredentials;
    }

    private string SelectedAuthenticationType
    {
        get
        {
            var selected = SqlAuthenticationComboBox.SelectedItem as string;
            return NormalizeAuthType(selected);
        }
    }

    private async Task<bool> ValidateConnectionAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var token = cts.Token;

            return _selectedConnectionType switch
            {
                SqlServerType => await ValidateSqlConnectionAsync(token),
                FabricType => await ValidateFabricConnectionAsync(token),
                BlobType => await ValidateBlobConnectionAsync(token),
                _ => false
            };
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Validation timed out.";
            return false;
        }
        catch (RequestFailedException ex)
        {
            StatusText.Text = $"Validation failed: {ex.Message}";
            return false;
        }
        catch (SqlException ex)
        {
            StatusText.Text = $"Validation failed: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Validation failed: {ex.Message}";
            return false;
        }
    }

    private async Task<bool> ValidateSqlConnectionAsync(CancellationToken cancellationToken)
    {
        var serverName = SqlServerNameTextBox.Text?.Trim() ?? string.Empty;
        var database = SqlDatabaseTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(serverName) || string.IsNullOrWhiteSpace(database))
        {
            StatusText.Text = "Server Name and Database are required for SQL Server.";
            return false;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = serverName,
            InitialCatalog = database,
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 12
        };

        if (SelectedAuthenticationType == EntraAuthType)
        {
            builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
        }
        else
        {
            var userName = SqlUserNameTextBox.Text?.Trim() ?? string.Empty;
            var password = SqlPasswordBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                StatusText.Text = "User Name and Password are required for SQL Server authentication.";
                return false;
            }

            builder.UserID = userName;
            builder.Password = password;
        }

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return true;
    }

    private async Task<bool> ValidateFabricConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = FabricConnectionStringTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            StatusText.Text = "Connection String is required for Azure Fabric.";
            return false;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return true;
    }

    private async Task<bool> ValidateBlobConnectionAsync(CancellationToken cancellationToken)
    {
        var storageAccount = BlobStorageAccountTextBox.Text?.Trim() ?? string.Empty;
        var container = BlobContainerTextBox.Text?.Trim() ?? string.Empty;
        var accessKey = BlobAccessKeyBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(storageAccount)
            || string.IsNullOrWhiteSpace(container)
            || string.IsNullOrWhiteSpace(accessKey))
        {
            StatusText.Text = "Storage Account, Container, and Access Key are required for Azure Blob.";
            return false;
        }

        var connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccount};AccountKey={accessKey};EndpointSuffix=core.windows.net";
        var containerClient = new BlobContainerClient(connectionString, container);
        var exists = await containerClient.ExistsAsync(cancellationToken);
        if (!exists.Value)
        {
            StatusText.Text = "Validation failed: container does not exist.";
            return false;
        }

        return true;
    }

    private void ApplyFieldsToItem(ConnectionItem item)
    {
        item.Type = _selectedConnectionType;
        item.SqlServerName = SqlServerNameTextBox.Text?.Trim() ?? string.Empty;
        item.SqlAuthenticationType = SelectedAuthenticationType;
        item.SqlUserName = SqlUserNameTextBox.Text?.Trim() ?? string.Empty;
        item.SqlPassword = SqlPasswordBox.Text?.Trim() ?? string.Empty;
        item.SqlDatabase = SqlDatabaseTextBox.Text?.Trim() ?? string.Empty;
        item.FabricConnectionString = FabricConnectionStringTextBox.Text?.Trim() ?? string.Empty;
        item.BlobStorageAccount = BlobStorageAccountTextBox.Text?.Trim() ?? string.Empty;
        item.BlobContainer = BlobContainerTextBox.Text?.Trim() ?? string.Empty;
        item.BlobAccessKey = BlobAccessKeyBox.Text?.Trim() ?? string.Empty;
        item.Endpoint = BuildEndpoint(item);
    }

    private static string BuildEndpoint(ConnectionItem item)
    {
        return item.Type switch
        {
            SqlServerType => item.SqlServerName,
            FabricType => item.FabricConnectionString,
            BlobType => item.BlobStorageAccount,
            _ => string.Empty
        };
    }

    private void ResetValidationState()
    {
        if (!_isValidated)
        {
            return;
        }

        _isValidated = false;
        SaveButton.Content = "Validate";
        StatusText.Text = "Connection details changed. Validate again before saving.";
    }

    private static void SetClass(Control element, string className, bool enabled)
    {
        if (enabled)
        {
            element.Classes.Add(className);
            return;
        }

        element.Classes.Remove(className);
    }

    private static string NormalizeType(string? type)
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
        if (string.Equals(authType, EntraAuthType, StringComparison.OrdinalIgnoreCase))
        {
            return EntraAuthType;
        }

        return SqlServerAuthType;
    }

    private string BuildUniqueName(string originalName)
    {
        var seed = string.IsNullOrWhiteSpace(originalName) ? "Connection" : originalName.Trim();
        var candidate = $"{seed} Copy";
        var suffix = 2;
        while (_items.Any(item => item.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{seed} Copy {suffix}";
            suffix++;
        }

        return candidate;
    }

    private string EnsureUniqueName(string baseName, ConnectionItem? currentItem)
    {
        var normalized = string.IsNullOrWhiteSpace(baseName) ? "Connection" : baseName.Trim();
        if (!_items.Any(item => !ReferenceEquals(item, currentItem) && item.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return normalized;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = $"{normalized} ({suffix})";
            if (!_items.Any(item => !ReferenceEquals(item, currentItem) && item.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }

            suffix++;
        }
    }
}
