using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DataProtectionTool.ClientApp.Models;
using DataProtectionTool.ClientApp.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DataProtectionTool.ClientApp.Views;

public partial class ConnectionsWizard : UserControl
{
    private const string SqlServerType = ConnectionsWizardViewModel.SqlServerType;
    private const string FabricType = ConnectionsWizardViewModel.FabricType;
    private const string BlobType = ConnectionsWizardViewModel.BlobType;
    private const string EntraAuthType = ConnectionsWizardViewModel.EntraAuthType;
    private const string SqlServerAuthType = ConnectionsWizardViewModel.SqlServerAuthType;

    private readonly ConnectionsWizardViewModel _viewModel = new();
    private readonly ObservableCollection<ConnectionItem> _items;
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
        DataContext = _viewModel;
        _items = _viewModel.Items;
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
        _viewModel.LoadItems();

        ItemsListBox.SelectedItem = null;
        _selectedItem = null;
        _editingItem = null;
        PopulateFieldsFromSelected();
    }

    private void SaveItems()
    {
        _viewModel.SaveItems();
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
        var draft = BuildDraftFromInput();
        var result = await _viewModel.ValidateConnectionAsync(draft, _selectedConnectionType, SelectedAuthenticationType);
        StatusText.Text = result.Message;
        return result.IsValid;
    }

    private ConnectionItem BuildDraftFromInput()
    {
        return new ConnectionItem
        {
            Name = NameTextBox.Text?.Trim() ?? string.Empty,
            Type = _selectedConnectionType,
            SqlServerName = SqlServerNameTextBox.Text?.Trim() ?? string.Empty,
            SqlAuthenticationType = SelectedAuthenticationType,
            SqlUserName = SqlUserNameTextBox.Text?.Trim() ?? string.Empty,
            SqlPassword = SqlPasswordBox.Text?.Trim() ?? string.Empty,
            SqlDatabase = SqlDatabaseTextBox.Text?.Trim() ?? string.Empty,
            FabricConnectionString = FabricConnectionStringTextBox.Text?.Trim() ?? string.Empty,
            BlobStorageAccount = BlobStorageAccountTextBox.Text?.Trim() ?? string.Empty,
            BlobContainer = BlobContainerTextBox.Text?.Trim() ?? string.Empty,
            BlobAccessKey = BlobAccessKeyBox.Text?.Trim() ?? string.Empty
        };
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

    private string BuildEndpoint(ConnectionItem item)
    {
        return _viewModel.BuildEndpoint(item, item.Type);
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

    private string NormalizeType(string? type)
    {
        return _viewModel.NormalizeType(type);
    }

    private string NormalizeAuthType(string? authType)
    {
        return _viewModel.NormalizeAuthType(authType);
    }

    private string BuildUniqueName(string originalName)
    {
        return _viewModel.BuildUniqueName(originalName);
    }

    private string EnsureUniqueName(string baseName, ConnectionItem? currentItem)
    {
        return _viewModel.EnsureUniqueName(baseName, currentItem);
    }
}
