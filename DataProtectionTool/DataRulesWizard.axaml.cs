using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DataProtectionTool;

public partial class DataRulesWizard : UserControl
{
    private readonly ObservableCollection<DataRuleRecord> _items = [];
    private DataRuleRecord? _selectedItem;
    private DataRuleRecord? _editingItem;
    private bool _isEditMode;
    private bool _pendingDeleteConfirmation;

    public DataRulesWizard()
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
        RuleNameTextBox.Focus();
        RefreshUi();
    }

    private void OnItemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedItem = ItemsListBox.SelectedItem as DataRuleRecord;
        _editingItem = _selectedItem;
        _isEditMode = false;
        _pendingDeleteConfirmation = false;
        DeleteButton.Content = "Delete";
        SetTextBoxesReadOnly(true);
        PopulateFieldsFromSelected();
        RefreshUi();
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
        RuleNameTextBox.Focus();
        RefreshUi();
    }

    private void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedItem is null)
        {
            return;
        }

        var copy = new DataRuleRecord
        {
            RuleName = BuildUniqueName(_selectedItem.RuleName),
            Description = _selectedItem.Description,
            Severity = _selectedItem.Severity,
            AppliesTo = _selectedItem.AppliesTo
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
            StatusText.Text = "Click Confirm Delete to remove this rule.";
            return;
        }

        _items.Remove(_selectedItem);
        SaveItems();
        _pendingDeleteConfirmation = false;
        DeleteButton.Content = "Delete";
        _selectedItem = null;
        _editingItem = null;
        ItemsListBox.SelectedItem = _items.FirstOrDefault();
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
        var name = RuleNameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText.Text = "Rule Name is required.";
            return;
        }

        if (_editingItem is null)
        {
            var newItem = new DataRuleRecord
            {
                RuleName = EnsureUniqueName(name, null),
                Description = DescriptionTextBox.Text?.Trim() ?? string.Empty,
                Severity = SeverityTextBox.Text?.Trim() ?? string.Empty,
                AppliesTo = AppliesToTextBox.Text?.Trim() ?? string.Empty
            };
            _items.Add(newItem);
            SaveItems();
            ItemsListBox.SelectedItem = newItem;
            StatusText.Text = "Data rule created.";
        }
        else
        {
            _editingItem.RuleName = EnsureUniqueName(name, _editingItem);
            _editingItem.Description = DescriptionTextBox.Text?.Trim() ?? string.Empty;
            _editingItem.Severity = SeverityTextBox.Text?.Trim() ?? string.Empty;
            _editingItem.AppliesTo = AppliesToTextBox.Text?.Trim() ?? string.Empty;
            SaveItems();
            ItemsListBox.ItemsSource = null;
            ItemsListBox.ItemsSource = _items;
            ItemsListBox.SelectedItem = _editingItem;
            StatusText.Text = "Data rule updated.";
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
        foreach (var item in DataRuleConfigurationStore.Load())
        {
            _items.Add(item);
        }

        ItemsListBox.SelectedItem = _items.FirstOrDefault();
        _selectedItem = ItemsListBox.SelectedItem as DataRuleRecord;
        _editingItem = _selectedItem;
        PopulateFieldsFromSelected();
    }

    private void SaveItems()
    {
        DataRuleConfigurationStore.Save(_items);
    }

    private void PopulateFieldsFromSelected()
    {
        if (_selectedItem is null)
        {
            ClearFields();
            return;
        }

        RuleNameTextBox.Text = _selectedItem.RuleName;
        DescriptionTextBox.Text = _selectedItem.Description;
        SeverityTextBox.Text = _selectedItem.Severity;
        AppliesToTextBox.Text = _selectedItem.AppliesTo;
    }

    private void ClearFields()
    {
        RuleNameTextBox.Text = string.Empty;
        DescriptionTextBox.Text = string.Empty;
        SeverityTextBox.Text = string.Empty;
        AppliesToTextBox.Text = string.Empty;
    }

    private void SetTextBoxesReadOnly(bool isReadOnly)
    {
        RuleNameTextBox.IsReadOnly = isReadOnly;
        DescriptionTextBox.IsReadOnly = isReadOnly;
        SeverityTextBox.IsReadOnly = isReadOnly;
        AppliesToTextBox.IsReadOnly = isReadOnly;
    }

    private void RefreshUi()
    {
        var hasItems = _items.Count > 0;
        EmptyStateText.IsVisible = !hasItems;
        ItemsListBox.IsVisible = hasItems;

        var hasSelection = _selectedItem is not null;
        EditButton.IsEnabled = hasSelection && !_isEditMode;
        CopyButton.IsEnabled = hasSelection && !_isEditMode;
        DeleteButton.IsEnabled = hasSelection && !_isEditMode;
        SaveButton.IsVisible = _isEditMode;
        CancelEditButton.IsVisible = _isEditMode;

        if (!_isEditMode && hasSelection)
        {
            StatusText.Text = "Select Edit to modify this rule.";
        }
        else if (!_isEditMode)
        {
            StatusText.Text = "Select a rule or click Add New.";
        }
    }

    private string BuildUniqueName(string originalName)
    {
        var seed = string.IsNullOrWhiteSpace(originalName) ? "Rule" : originalName.Trim();
        var candidate = $"{seed} Copy";
        var suffix = 2;
        while (_items.Any(item => item.RuleName.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{seed} Copy {suffix}";
            suffix++;
        }

        return candidate;
    }

    private string EnsureUniqueName(string baseName, DataRuleRecord? currentItem)
    {
        var normalized = string.IsNullOrWhiteSpace(baseName) ? "Rule" : baseName.Trim();
        if (!_items.Any(item => !ReferenceEquals(item, currentItem) && item.RuleName.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return normalized;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = $"{normalized} ({suffix})";
            if (!_items.Any(item => !ReferenceEquals(item, currentItem) && item.RuleName.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }

            suffix++;
        }
    }
}
