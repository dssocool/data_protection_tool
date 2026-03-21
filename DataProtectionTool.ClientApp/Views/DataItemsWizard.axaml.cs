using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
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

namespace DataProtectionTool.ClientApp.Views;

public partial class DataItemsWizard : UserControl
{
    private const string SqlServerType = "Microsoft SQL Server";
    private const string FabricType = "Azure Fabric";
    private const string BlobType = "Azure Blob Storage";
    private const string EntraAuthType = "Entra";

    private readonly ObservableCollection<DataItemRecord> _items = [];
    private readonly ObservableCollection<ConnectionChip> _selectedConnectionChips = [];
    private readonly ObservableCollection<ConnectionSuggestion> _connectionSuggestions = [];
    private readonly ObservableCollection<ConnectionItemsPickerModel> _connectionItemPickers = [];
    private readonly Dictionary<string, ConnectionItem> _connectionsByName = new(StringComparer.OrdinalIgnoreCase);

    private DataItemRecord? _selectedItem;
    private DataItemRecord? _editingItem;
    private bool _isEditMode;
    private bool _pendingDeleteConfirmation;
    private bool _hasFetchedForCurrentSession;

    public DataItemsWizard()
    {
        InitializeComponent();
        ItemsListBox.ItemsSource = _items;
        SelectedConnectionsItemsControl.ItemsSource = _selectedConnectionChips;
        ConnectionSuggestionsListBox.ItemsSource = _connectionSuggestions;
        PerConnectionPickersItemsControl.ItemsSource = _connectionItemPickers;
        LoadConnections();
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
        _hasFetchedForCurrentSession = false;
        DeleteButton.Content = "Delete";
        ClearFields();
        RefreshUi();
        ItemNameTextBox.Focus();
    }

    private void OnItemRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control row || row.DataContext is not DataItemRecord item)
        {
            return;
        }

        _isEditMode = false;
        _pendingDeleteConfirmation = false;
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
        _hasFetchedForCurrentSession = true;
        DeleteButton.Content = "Delete";
        RefreshUi();
        ItemNameTextBox.Focus();
    }

    private void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedItem is null)
        {
            return;
        }

        var copy = new DataItemRecord
        {
            ItemName = BuildUniqueName(_selectedItem.ItemName),
            Source = _selectedItem.Source,
            SelectedConnections = [.. _selectedItem.SelectedConnections],
            SelectedItems = [.. _selectedItem.SelectedItems],
            SelectedItemKinds = [.. _selectedItem.SelectedItemKinds],
            SelectedItemConnections = [.. _selectedItem.SelectedItemConnections]
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
            StatusText.Text = "Click Confirm Delete to remove this data item.";
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
        DeleteButton.Content = "Delete";
        PopulateFieldsFromSelected();
        HideConnectionSuggestions();
        HideAllPerConnectionSuggestions();
        RefreshUi();
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var name = ItemNameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText.Text = "Name is required.";
            return;
        }

        var selectedConnections = _selectedConnectionChips
            .Select(static chip => chip.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selectedItems = new List<string>();
        var selectedItemKinds = new List<string>();
        var selectedItemConnections = new List<string>();
        foreach (var picker in _connectionItemPickers)
        {
            foreach (var item in picker.SelectedItems)
            {
                selectedItems.Add(item.Name);
                selectedItemKinds.Add(item.Kind);
                selectedItemConnections.Add(item.ConnectionName);
            }
        }

        if (_editingItem is null)
        {
            var newItem = new DataItemRecord
            {
                ItemName = EnsureUniqueName(name, null),
                Source = string.Join(", ", selectedConnections),
                SelectedConnections = selectedConnections,
                SelectedItems = selectedItems,
                SelectedItemKinds = selectedItemKinds,
                SelectedItemConnections = selectedItemConnections
            };
            _items.Add(newItem);
            SaveItems();
            ItemsListBox.SelectedItem = newItem;
            StatusText.Text = "Data item created.";
        }
        else
        {
            _editingItem.ItemName = EnsureUniqueName(name, _editingItem);
            _editingItem.Source = string.Join(", ", selectedConnections);
            _editingItem.SelectedConnections = selectedConnections;
            _editingItem.SelectedItems = selectedItems;
            _editingItem.SelectedItemKinds = selectedItemKinds;
            _editingItem.SelectedItemConnections = selectedItemConnections;
            SaveItems();
            ItemsListBox.ItemsSource = null;
            ItemsListBox.ItemsSource = _items;
            ItemsListBox.SelectedItem = _editingItem;
            StatusText.Text = "Data item updated.";
        }

        _isEditMode = false;
        _pendingDeleteConfirmation = false;
        DeleteButton.Content = "Delete";
        PopulateFieldsFromSelected();
        HideConnectionSuggestions();
        HideAllPerConnectionSuggestions();
        RefreshUi();
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ConnectionSuggestionsPopup.IsVisible
            && !IsInsideControl(e.Source, ConnectionPickerSurface)
            && !IsInsideControl(e.Source, ConnectionSuggestionsPopup))
        {
            HideConnectionSuggestions();
        }

        if (!IsInsidePerConnectionPickerArea(e.Source))
        {
            HideAllPerConnectionSuggestions();
        }
    }

    private void OnConnectionPickerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_isEditMode)
        {
            return;
        }

        ShowConnectionSuggestions();
        ConnectionSearchTextBox.Focus();
        e.Handled = true;
    }

    private void OnConnectionSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_isEditMode)
        {
            return;
        }

        ShowConnectionSuggestions();
        RefreshConnectionSuggestions();
    }

    private void OnConnectionSuggestionPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_isEditMode || sender is not Control control || control.DataContext is not ConnectionSuggestion suggestion)
        {
            return;
        }

        if (_selectedConnectionChips.Any(item => item.Name.Equals(suggestion.Name, StringComparison.OrdinalIgnoreCase)))
        {
            RemoveConnectionChipByName(suggestion.Name);
        }
        else
        {
            _selectedConnectionChips.Add(new ConnectionChip { Name = suggestion.Name, Type = suggestion.Type });
            EnsureConnectionPickerExists(suggestion.Name);
        }

        ConnectionSearchTextBox.Text = string.Empty;
        RefreshConnectionSuggestions();
        RefreshFetchButtonState();
        e.Handled = true;
    }

    private void OnRemoveConnectionChipClicked(object? sender, RoutedEventArgs e)
    {
        if (!_isEditMode || sender is not Control control || control.DataContext is not ConnectionChip chip)
        {
            return;
        }

        RemoveConnectionChipByName(chip.Name);
        RefreshConnectionSuggestions();
        RefreshFetchButtonState();
    }

    private async void OnFetchClicked(object? sender, RoutedEventArgs e)
    {
        if (!_isEditMode || _selectedConnectionChips.Count == 0)
        {
            return;
        }

        FetchButton.IsEnabled = false;
        StatusText.Text = "Fetching items from selected connections...";
        var failures = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var token = cts.Token;

        foreach (var chip in _selectedConnectionChips)
        {
            var picker = EnsureConnectionPickerExists(chip.Name);
            picker.AvailableItems.Clear();
            picker.FilteredSuggestions.Clear();

            if (!_connectionsByName.TryGetValue(chip.Name, out var connection))
            {
                failures.Add($"{chip.Name}: connection definition not found.");
                continue;
            }

            try
            {
                var items = await FetchItemsForConnectionAsync(connection, token);
                foreach (var item in items)
                {
                    picker.AvailableItems.Add(item);
                }

                picker.SyncSelectedWithAvailable();
                picker.RefreshFilteredSuggestions();
            }
            catch (Exception ex)
            {
                failures.Add($"{chip.Name}: {ex.Message}");
            }
        }

        _hasFetchedForCurrentSession = true;
        RefreshPerConnectionVisibility();
        RefreshFetchButtonState();

        if (failures.Count == 0)
        {
            var total = _connectionItemPickers.Sum(static picker => picker.AvailableItems.Count);
            StatusText.Text = $"Fetched {total} item(s).";
        }
        else
        {
            StatusText.Text = $"Fetch completed with issues: {string.Join(" | ", failures.Take(2))}";
        }
    }

    private void OnPerConnectionPickerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_isEditMode || sender is not Control control || control.DataContext is not ConnectionItemsPickerModel picker)
        {
            return;
        }

        HideAllPerConnectionSuggestions();
        picker.IsSuggestionsVisible = picker.FilteredSuggestions.Count > 0;
        e.Handled = true;
    }

    private void OnPerConnectionSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_isEditMode || sender is not TextBox textBox || textBox.DataContext is not ConnectionItemsPickerModel picker)
        {
            return;
        }

        picker.SearchText = textBox.Text ?? string.Empty;
        picker.RefreshFilteredSuggestions();
        HideAllPerConnectionSuggestions();
        picker.IsSuggestionsVisible = picker.FilteredSuggestions.Count > 0;
    }

    private void OnPerConnectionSuggestionPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_isEditMode || sender is not Control control || control.DataContext is not FetchedItemOption option)
        {
            return;
        }

        var picker = _connectionItemPickers.FirstOrDefault(item => item.ConnectionName.Equals(option.ConnectionName, StringComparison.OrdinalIgnoreCase));
        if (picker is null)
        {
            return;
        }

        picker.AddSelected(option);
        picker.SearchText = string.Empty;
        picker.RefreshFilteredSuggestions();
        picker.IsSuggestionsVisible = picker.FilteredSuggestions.Count > 0;
        e.Handled = true;
    }

    private void OnRemovePerConnectionItemClicked(object? sender, RoutedEventArgs e)
    {
        if (!_isEditMode || sender is not Control control || control.DataContext is not FetchedItemOption selected)
        {
            return;
        }

        var picker = _connectionItemPickers.FirstOrDefault(item => item.ConnectionName.Equals(selected.ConnectionName, StringComparison.OrdinalIgnoreCase));
        picker?.RemoveSelected(selected);
    }

    private void LoadItems()
    {
        _items.Clear();
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

            _items.Add(item);
        }

        ItemsListBox.SelectedItem = null;
        _selectedItem = null;
        _editingItem = null;
        PopulateFieldsFromSelected();
    }

    private void SaveItems()
    {
        DataItemConfigurationStore.Save(_items);
    }

    private void LoadConnections()
    {
        _connectionsByName.Clear();
        foreach (var connection in ConnectionConfigurationStore.Load())
        {
            if (string.IsNullOrWhiteSpace(connection.Name))
            {
                continue;
            }

            _connectionsByName[connection.Name] = connection;
        }
    }

    private void PopulateFieldsFromSelected()
    {
        _selectedConnectionChips.Clear();
        _connectionItemPickers.Clear();
        _connectionSuggestions.Clear();

        if (_selectedItem is null)
        {
            ClearFields();
            return;
        }

        ItemNameTextBox.Text = _selectedItem.ItemName;
        foreach (var connectionName in _selectedItem.SelectedConnections.Where(static n => !string.IsNullOrWhiteSpace(n)))
        {
            var type = _connectionsByName.TryGetValue(connectionName, out var connection) ? connection.Type : string.Empty;
            _selectedConnectionChips.Add(new ConnectionChip { Name = connectionName, Type = type });
            EnsureConnectionPickerExists(connectionName);
        }

        for (var index = 0; index < _selectedItem.SelectedItems.Count; index++)
        {
            var name = _selectedItem.SelectedItems[index];
            var kind = index < _selectedItem.SelectedItemKinds.Count ? _selectedItem.SelectedItemKinds[index] : string.Empty;
            var connectionName = index < _selectedItem.SelectedItemConnections.Count ? _selectedItem.SelectedItemConnections[index] : string.Empty;
            var targetPicker = string.IsNullOrWhiteSpace(connectionName)
                ? _connectionItemPickers.FirstOrDefault()
                : _connectionItemPickers.FirstOrDefault(picker => picker.ConnectionName.Equals(connectionName, StringComparison.OrdinalIgnoreCase));
            targetPicker?.SelectedItems.Add(FetchedItemOption.FromSaved(name, kind, targetPicker.ConnectionName));
        }

        _hasFetchedForCurrentSession = _connectionItemPickers.Any(static picker => picker.SelectedItems.Count > 0);
        ConnectionSearchTextBox.Text = string.Empty;
        RefreshConnectionSuggestions();
        RefreshPerConnectionVisibility();
        RefreshFetchButtonState();
    }

    private void ClearFields()
    {
        ItemNameTextBox.Text = string.Empty;
        ConnectionSearchTextBox.Text = string.Empty;
        _selectedConnectionChips.Clear();
        _connectionSuggestions.Clear();
        _connectionItemPickers.Clear();
        _hasFetchedForCurrentSession = false;
        HideConnectionSuggestions();
        RefreshPerConnectionVisibility();
        RefreshFetchButtonState();
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
        CancelEditButton.IsVisible = _isEditMode;

        ItemNameTextBox.IsReadOnly = !_isEditMode;
        ConnectionSearchTextBox.IsReadOnly = !_isEditMode;
        ConnectionPickerSurface.IsEnabled = _isEditMode;
        ConnectionSuggestionsListBox.IsEnabled = _isEditMode;
        PerConnectionPickersItemsControl.IsEnabled = _isEditMode;
        RefreshPerConnectionVisibility();
        RefreshFetchButtonState();

        if (!_isEditMode && hasSelection)
        {
            StatusText.Text = "Select Edit to modify this data item.";
        }
        else if (!_isEditMode)
        {
            StatusText.Text = "Select a data item or click Add New.";
        }
    }

    private void RemoveConnectionChipByName(string name)
    {
        var chip = _selectedConnectionChips.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (chip is not null)
        {
            _selectedConnectionChips.Remove(chip);
        }

        var picker = _connectionItemPickers.FirstOrDefault(item => item.ConnectionName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (picker is not null)
        {
            _connectionItemPickers.Remove(picker);
        }

        if (_connectionItemPickers.Count == 0)
        {
            _hasFetchedForCurrentSession = false;
        }

        RefreshPerConnectionVisibility();
    }

    private ConnectionItemsPickerModel EnsureConnectionPickerExists(string connectionName)
    {
        var picker = _connectionItemPickers.FirstOrDefault(item => item.ConnectionName.Equals(connectionName, StringComparison.OrdinalIgnoreCase));
        if (picker is not null)
        {
            return picker;
        }

        picker = new ConnectionItemsPickerModel(connectionName);
        _connectionItemPickers.Add(picker);
        return picker;
    }

    private void RefreshConnectionSuggestions()
    {
        _connectionSuggestions.Clear();
        var query = ConnectionSearchTextBox.Text?.Trim() ?? string.Empty;

        var suggestions = _connectionsByName.Values
            .Where(connection => !_selectedConnectionChips.Any(chip => chip.Name.Equals(connection.Name, StringComparison.OrdinalIgnoreCase)))
            .Where(connection => string.IsNullOrWhiteSpace(query)
                || connection.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || connection.Type.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(connection => connection.Name, StringComparer.OrdinalIgnoreCase)
            .Select(connection => new ConnectionSuggestion
            {
                Name = connection.Name,
                Type = connection.Type
            });

        foreach (var suggestion in suggestions)
        {
            _connectionSuggestions.Add(suggestion);
        }
    }

    private void ShowConnectionSuggestions()
    {
        RefreshConnectionSuggestions();
        ConnectionSuggestionsPopup.IsVisible = _isEditMode && _connectionSuggestions.Count > 0;
    }

    private void HideConnectionSuggestions()
    {
        ConnectionSuggestionsPopup.IsVisible = false;
    }

    private void HideAllPerConnectionSuggestions()
    {
        foreach (var picker in _connectionItemPickers)
        {
            picker.IsSuggestionsVisible = false;
        }
    }

    private void RefreshPerConnectionVisibility()
    {
        PerConnectionPickersHost.IsVisible = _isEditMode && (_hasFetchedForCurrentSession || _editingItem is not null) && _connectionItemPickers.Count > 0;
    }

    private void RefreshFetchButtonState()
    {
        FetchButton.IsEnabled = _isEditMode && _selectedConnectionChips.Count > 0;
    }

    private static bool IsInsideControl(object? source, Control? target)
    {
        if (target is null)
        {
            return false;
        }

        for (var current = source as ILogical; current is not null; current = current.LogicalParent)
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsidePerConnectionPickerArea(object? source)
    {
        for (var current = source as ILogical; current is not null; current = current.LogicalParent)
        {
            if (current is Control control
                && (control.DataContext is ConnectionItemsPickerModel || control.DataContext is FetchedItemOption))
            {
                return true;
            }
        }

        return false;
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

    private async Task<List<FetchedItemOption>> FetchItemsForConnectionAsync(ConnectionItem connection, CancellationToken token)
    {
        var type = NormalizeConnectionType(connection.Type);
        return type switch
        {
            BlobType => await FetchBlobItemsAsync(connection, token),
            FabricType => await FetchSqlItemsAsync(connection, fabricMode: true, token),
            _ => await FetchSqlItemsAsync(connection, fabricMode: false, token)
        };
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

    private string BuildUniqueName(string originalName)
    {
        var seed = string.IsNullOrWhiteSpace(originalName) ? "Data Item" : originalName.Trim();
        var candidate = $"{seed} Copy";
        var suffix = 2;
        while (_items.Any(item => item.ItemName.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{seed} Copy {suffix}";
            suffix++;
        }

        return candidate;
    }

    private string EnsureUniqueName(string baseName, DataItemRecord? currentItem)
    {
        var normalized = string.IsNullOrWhiteSpace(baseName) ? "Data Item" : baseName.Trim();
        if (!_items.Any(item => !ReferenceEquals(item, currentItem) && item.ItemName.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return normalized;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = $"{normalized} ({suffix})";
            if (!_items.Any(item => !ReferenceEquals(item, currentItem) && item.ItemName.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }

            suffix++;
        }
    }
}
