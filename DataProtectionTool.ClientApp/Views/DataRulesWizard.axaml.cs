using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DataProtectionTool.ClientApp.Models;
using DataProtectionTool.ClientApp.ViewModels;
using System;
using System.Collections.ObjectModel;

namespace DataProtectionTool.ClientApp.Views;

public partial class DataRulesWizard : UserControl
{
    private readonly DataRulesWizardViewModel _viewModel = new();
    private readonly ObservableCollection<DataRuleRecord> _items;
    private DataRuleRecord? _selectedItem;
    private DataRuleRecord? _editingItem;
    private bool _isEditMode;
    private bool _pendingDeleteConfirmation;

    public DataRulesWizard()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _items = _viewModel.Items;
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

    private void OnItemRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control row || row.DataContext is not DataRuleRecord item)
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
        DetailsPanel.IsVisible = hasSelection || _isEditMode;
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
        return _viewModel.BuildUniqueName(originalName);
    }

    private string EnsureUniqueName(string baseName, DataRuleRecord? currentItem)
    {
        return _viewModel.EnsureUniqueName(baseName, currentItem);
    }
}
