using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DataProtectionTool.ClientApp.Models;
using DataProtectionTool.ClientApp.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DataProtectionTool.ClientApp.Views;

public partial class FlowsWizard : UserControl
{
    private readonly FlowsWizardViewModel _viewModel = new();
    private readonly ObservableCollection<FlowListItem> _items;
    private FlowListItem? _selectedItem;
    private FlowListItem? _editingItem;
    private bool _isEditMode;
    private bool _pendingDeleteConfirmation;

    public event EventHandler? FlowsChanged;

    public FlowsWizard()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _items = _viewModel.Items;
        ItemsListBox.ItemsSource = _items;
        LoadItems();
        RefreshUi();
    }

    public void ResetForCreate()
    {
        _isEditMode = false;
        _selectedItem = null;
        _editingItem = null;
        _pendingDeleteConfirmation = false;
        DeleteButton.Content = "Delete";
        SetTextBoxesReadOnly(true);
        ItemsListBox.SelectedItem = null;
        PopulateFieldsFromSelected();
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
        FlowNameTextBox.Focus();
        RefreshUi();
    }

    private void OnItemRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control row || row.DataContext is not FlowListItem item)
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
        FlowNameTextBox.Focus();
        RefreshUi();
    }

    private void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedItem is null)
        {
            return;
        }

        var copy = new FlowListItem
        {
            FlowName = BuildUniqueName(_selectedItem.FlowName, item => item.FlowName),
            Domain = _selectedItem.Domain,
            Source = _selectedItem.Source,
            Destination = _selectedItem.Destination,
            Status = _selectedItem.Status,
            Action = _selectedItem.Action,
            DataItems = _selectedItem.DataItems,
            DataRules = _selectedItem.DataRules
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
            StatusText.Text = "Click Confirm Delete to remove this flow.";
            return;
        }

        var toDelete = _selectedItem;
        _pendingDeleteConfirmation = false;
        DeleteButton.Content = "Delete";
        _items.Remove(toDelete);
        SaveItems();
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
        var flowName = FlowNameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(flowName))
        {
            StatusText.Text = "Flow Name is required.";
            return;
        }

        if (_editingItem is null)
        {
            var newItem = new FlowListItem
            {
                FlowName = EnsureUniqueFlowName(flowName, null),
                Source = SourceTextBox.Text?.Trim() ?? string.Empty,
                Destination = DestinationTextBox.Text?.Trim() ?? string.Empty,
                DataItems = DataItemsTextBox.Text?.Trim() ?? string.Empty,
                DataRules = DataRulesTextBox.Text?.Trim() ?? string.Empty
            };
            _items.Add(newItem);
            SaveItems();
            ItemsListBox.SelectedItem = newItem;
            StatusText.Text = "Flow created.";
        }
        else
        {
            _editingItem.FlowName = EnsureUniqueFlowName(flowName, _editingItem);
            _editingItem.Source = SourceTextBox.Text?.Trim() ?? string.Empty;
            _editingItem.Destination = DestinationTextBox.Text?.Trim() ?? string.Empty;
            _editingItem.DataItems = DataItemsTextBox.Text?.Trim() ?? string.Empty;
            _editingItem.DataRules = DataRulesTextBox.Text?.Trim() ?? string.Empty;
            SaveItems();
            ItemsListBox.ItemsSource = null;
            ItemsListBox.ItemsSource = _items;
            ItemsListBox.SelectedItem = _editingItem;
            StatusText.Text = "Flow updated.";
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
        FlowsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PopulateFieldsFromSelected()
    {
        if (_selectedItem is null)
        {
            ClearFields();
            return;
        }

        FlowNameTextBox.Text = _selectedItem.FlowName;
        SourceTextBox.Text = _selectedItem.Source;
        DestinationTextBox.Text = _selectedItem.Destination;
        DataItemsTextBox.Text = _selectedItem.DataItems;
        DataRulesTextBox.Text = _selectedItem.DataRules;
    }

    private void ClearFields()
    {
        FlowNameTextBox.Text = string.Empty;
        SourceTextBox.Text = string.Empty;
        DestinationTextBox.Text = string.Empty;
        DataItemsTextBox.Text = string.Empty;
        DataRulesTextBox.Text = string.Empty;
    }

    private void SetTextBoxesReadOnly(bool isReadOnly)
    {
        FlowNameTextBox.IsReadOnly = isReadOnly;
        SourceTextBox.IsReadOnly = isReadOnly;
        DestinationTextBox.IsReadOnly = isReadOnly;
        DataItemsTextBox.IsReadOnly = isReadOnly;
        DataRulesTextBox.IsReadOnly = isReadOnly;
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
            StatusText.Text = "Select Edit to modify this flow.";
        }
        else if (!_isEditMode)
        {
            StatusText.Text = "Select a flow or click Add New.";
        }
    }

    private string EnsureUniqueFlowName(string baseName, FlowListItem? currentItem)
    {
        return _viewModel.EnsureUniqueFlowName(baseName, currentItem);
    }

    private string BuildUniqueName(string name, Func<FlowListItem, string> selector)
    {
        return _viewModel.BuildUniqueName(name, selector);
    }
}
