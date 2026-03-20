using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DataProtectionTool;

public partial class DataItemsWizard : UserControl
{
    private readonly ObservableCollection<DataItemRecord> _items = [];
    private DataItemRecord? _selectedItem;
    private DataItemRecord? _editingItem;
    private bool _isEditMode;
    private bool _pendingDeleteConfirmation;

    public DataItemsWizard()
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
        ItemNameTextBox.Focus();
        RefreshUi();
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
        ItemNameTextBox.Focus();
        RefreshUi();
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
            Classification = _selectedItem.Classification,
            Source = _selectedItem.Source,
            Retention = _selectedItem.Retention
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
        SetTextBoxesReadOnly(true);
        PopulateFieldsFromSelected();
        RefreshUi();
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var name = ItemNameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText.Text = "Item Name is required.";
            return;
        }

        if (_editingItem is null)
        {
            var newItem = new DataItemRecord
            {
                ItemName = EnsureUniqueName(name, null),
                Classification = ClassificationTextBox.Text?.Trim() ?? string.Empty,
                Source = SourceTextBox.Text?.Trim() ?? string.Empty,
                Retention = RetentionTextBox.Text?.Trim() ?? string.Empty
            };
            _items.Add(newItem);
            SaveItems();
            ItemsListBox.SelectedItem = newItem;
            StatusText.Text = "Data item created.";
        }
        else
        {
            _editingItem.ItemName = EnsureUniqueName(name, _editingItem);
            _editingItem.Classification = ClassificationTextBox.Text?.Trim() ?? string.Empty;
            _editingItem.Source = SourceTextBox.Text?.Trim() ?? string.Empty;
            _editingItem.Retention = RetentionTextBox.Text?.Trim() ?? string.Empty;
            SaveItems();
            ItemsListBox.ItemsSource = null;
            ItemsListBox.ItemsSource = _items;
            ItemsListBox.SelectedItem = _editingItem;
            StatusText.Text = "Data item updated.";
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
        foreach (var item in DataItemConfigurationStore.Load())
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
        DataItemConfigurationStore.Save(_items);
    }

    private void PopulateFieldsFromSelected()
    {
        if (_selectedItem is null)
        {
            ClearFields();
            return;
        }

        ItemNameTextBox.Text = _selectedItem.ItemName;
        ClassificationTextBox.Text = _selectedItem.Classification;
        SourceTextBox.Text = _selectedItem.Source;
        RetentionTextBox.Text = _selectedItem.Retention;
    }

    private void ClearFields()
    {
        ItemNameTextBox.Text = string.Empty;
        ClassificationTextBox.Text = string.Empty;
        SourceTextBox.Text = string.Empty;
        RetentionTextBox.Text = string.Empty;
    }

    private void SetTextBoxesReadOnly(bool isReadOnly)
    {
        ItemNameTextBox.IsReadOnly = isReadOnly;
        ClassificationTextBox.IsReadOnly = isReadOnly;
        SourceTextBox.IsReadOnly = isReadOnly;
        RetentionTextBox.IsReadOnly = isReadOnly;
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
            StatusText.Text = "Select Edit to modify this data item.";
        }
        else if (!_isEditMode)
        {
            StatusText.Text = "Select a data item or click Add New.";
        }
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
