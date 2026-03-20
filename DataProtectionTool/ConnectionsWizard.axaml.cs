using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DataProtectionTool;

public partial class ConnectionsWizard : UserControl
{
    private readonly ObservableCollection<ConnectionItem> _items = [];
    private ConnectionItem? _selectedItem;
    private ConnectionItem? _editingItem;
    private bool _isEditMode;
    private bool _pendingDeleteConfirmation;

    public ConnectionsWizard()
    {
        InitializeComponent();
        ItemsListBox.ItemsSource = _items;
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
        DeleteButton.Content = "Delete";
        ClearFields();
        SetTextBoxesReadOnly(false);
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
        DeleteButton.Content = "Delete";
        SetTextBoxesReadOnly(true);

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
        DeleteButton.Content = "Delete";
        SetTextBoxesReadOnly(false);
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
            Endpoint = _selectedItem.Endpoint,
            Type = _selectedItem.Type,
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
        DeleteButton.Content = "Delete";
        SetTextBoxesReadOnly(true);
        PopulateFieldsFromSelected();
        RefreshUi();
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText.Text = "Name is required.";
            return;
        }

        if (_editingItem is null)
        {
            var newItem = new ConnectionItem
            {
                Name = EnsureUniqueName(name, null),
                Endpoint = EndpointTextBox.Text?.Trim() ?? string.Empty,
                Type = TypeTextBox.Text?.Trim() ?? string.Empty,
                Notes = NotesTextBox.Text?.Trim() ?? string.Empty
            };
            _items.Add(newItem);
            SaveItems();
            ItemsListBox.SelectedItem = newItem;
            StatusText.Text = "Connection created.";
        }
        else
        {
            _editingItem.Name = EnsureUniqueName(name, _editingItem);
            _editingItem.Endpoint = EndpointTextBox.Text?.Trim() ?? string.Empty;
            _editingItem.Type = TypeTextBox.Text?.Trim() ?? string.Empty;
            _editingItem.Notes = NotesTextBox.Text?.Trim() ?? string.Empty;
            SaveItems();
            ItemsListBox.ItemsSource = null;
            ItemsListBox.ItemsSource = _items;
            ItemsListBox.SelectedItem = _editingItem;
            StatusText.Text = "Connection updated.";
        }

        _isEditMode = false;
        _pendingDeleteConfirmation = false;
        DeleteButton.Content = "Delete";
        SetTextBoxesReadOnly(true);
        RefreshUi();
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
        if (_selectedItem is null)
        {
            ClearFields();
            return;
        }

        NameTextBox.Text = _selectedItem.Name;
        EndpointTextBox.Text = _selectedItem.Endpoint;
        TypeTextBox.Text = _selectedItem.Type;
        NotesTextBox.Text = _selectedItem.Notes;
    }

    private void ClearFields()
    {
        NameTextBox.Text = string.Empty;
        EndpointTextBox.Text = string.Empty;
        TypeTextBox.Text = string.Empty;
        NotesTextBox.Text = string.Empty;
    }

    private void SetTextBoxesReadOnly(bool isReadOnly)
    {
        NameTextBox.IsReadOnly = isReadOnly;
        EndpointTextBox.IsReadOnly = isReadOnly;
        TypeTextBox.IsReadOnly = isReadOnly;
        NotesTextBox.IsReadOnly = isReadOnly;
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

        if (!_isEditMode && hasSelection)
        {
            StatusText.Text = "Select Edit to modify this connection.";
        }
        else if (!_isEditMode)
        {
            StatusText.Text = "Select a connection or click Add New.";
        }
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
