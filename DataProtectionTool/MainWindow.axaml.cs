using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DataProtectionTool;

public partial class MainWindow : Window
{
    private enum WizardSection
    {
        Flows,
        Connections,
        DataItems,
        DataRules
    }

    private readonly ObservableCollection<FlowListItem> _flows = [];
    private readonly FlowsWizard _flowsWizard = new();
    private readonly ConnectionsWizard _connectionsWizard = new();
    private readonly DataItemsWizard _dataItemsWizard = new();
    private readonly DataRulesWizard _dataRulesWizard = new();
    private FlowListItem? _activePopoverFlow;
    private FlowListItem? _hoveredFlow;
    private FlowListItem? _selectedFlow;
    private bool _isPopoverPinned;
    private bool _isPointerInsidePopover;
    private readonly DispatcherTimer _hoverExitTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(140)
    };

    public MainWindow()
    {
        InitializeComponent();
        _hoverExitTimer.Tick += OnHoverExitTimerTick;
        _flowsWizard.FlowsChanged += OnFlowsWizardChanged;
        WizardContentHost.Content = _flowsWizard;
        LoadSavedFlows();
        ShowMainListPage();
    }

    private void OnNewItemClicked(object? sender, RoutedEventArgs e)
    {
        ShowNewItemPage();
        SelectWizardSection(WizardSection.Flows, resetFlowWizard: true);
    }

    private void OnBackToMainClicked(object? sender, RoutedEventArgs e)
    {
        ShowMainListPage();
    }

    private void OnMainListNavClicked(object? sender, RoutedEventArgs e)
    {
        ShowMainListPage();
    }

    private void OnGoToConfigurationWorkspaceClicked(object? sender, RoutedEventArgs e)
    {
        SelectWizardSection(WizardSection.Flows);
    }

    private void OnFlowsNavClicked(object? sender, RoutedEventArgs e)
    {
        SelectWizardSection(WizardSection.Flows);
    }

    private void OnConnectionsNavClicked(object? sender, RoutedEventArgs e)
    {
        SelectWizardSection(WizardSection.Connections);
    }

    private void OnDataItemsNavClicked(object? sender, RoutedEventArgs e)
    {
        SelectWizardSection(WizardSection.DataItems);
    }

    private void OnDataRulesNavClicked(object? sender, RoutedEventArgs e)
    {
        SelectWizardSection(WizardSection.DataRules);
    }

    private void OnFlowRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Control row || row.DataContext is not FlowListItem flow)
        {
            return;
        }

        _hoveredFlow = flow;
        UpdateRowHighlights();
        CancelHoverExitTimer();

        if (_isPopoverPinned)
        {
            return;
        }

        var mousePosition = e.GetPosition(FlowPopoverCanvas);
        ShowPopover(flow, row, mousePosition, pinned: false);
    }

    private void OnFlowRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not Control row || row.DataContext is not FlowListItem flow || _hoveredFlow != flow)
        {
            return;
        }

        _hoveredFlow = null;
        UpdateRowHighlights();

        if (_isPopoverPinned || _isPointerInsidePopover)
        {
            return;
        }

        _hoverExitTimer.Start();
    }

    private void OnPopoverPointerEntered(object? sender, PointerEventArgs e)
    {
        _isPointerInsidePopover = true;
        CancelHoverExitTimer();
    }

    private void OnPopoverPointerExited(object? sender, PointerEventArgs e)
    {
        _isPointerInsidePopover = false;
        if (!_isPopoverPinned && _hoveredFlow is null)
        {
            HidePopover();
        }
    }

    private void OnFlowRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control row || row.DataContext is not FlowListItem flow)
        {
            return;
        }

        _selectedFlow = flow;
        _hoveredFlow = flow;
        UpdateRowHighlights();

        var mousePosition = e.GetPosition(FlowPopoverCanvas);
        ShowPopover(flow, row, mousePosition, pinned: true);
        e.Handled = true;
    }

    private void OnPopoverPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!FlowPopoverCanvas.IsVisible)
        {
            return;
        }

        _isPopoverPinned = true;
        if (_activePopoverFlow is not null)
        {
            _selectedFlow = _activePopoverFlow;
            UpdateRowHighlights();
        }
        PopoverPinnedHintText.IsVisible = true;
        CancelHoverExitTimer();
        e.Handled = true;
    }

    private void OnFlowListContainerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_isPopoverPinned)
        {
            return;
        }

        var clickedFlowRow = GetFlowRowFromSource(e.Source);
        var clickedInsidePopover = IsInsideControl(e.Source, FlowHoverPopover);
        if (clickedFlowRow is not null || clickedInsidePopover)
        {
            return;
        }

        UnpinPopover();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_isPopoverPinned)
        {
            return;
        }

        UnpinPopover();
        _selectedFlow = null;
        UpdateRowHighlights();
        e.Handled = true;
    }

    private void OnFlowsWizardChanged(object? sender, EventArgs e)
    {
        LoadSavedFlows();
    }

    private void LoadSavedFlows()
    {
        _flows.Clear();
        foreach (var flow in FlowConfigurationStore.Load())
        {
            _flows.Add(flow);
        }

        FlowListItemsControl.ItemsSource = _flows;
        UpdateListUiState();
    }

    private void AddFlow(FlowDetailsInput details)
    {
        if (string.IsNullOrWhiteSpace(details.FlowName))
        {
            return;
        }

        var item = new FlowListItem
        {
            FlowName = details.FlowName,
            Source = details.SourceConnection,
            Destination = details.Destination,
            DataItems = details.DataItems,
            DataRules = details.DataRules
        };

        _flows.Add(item);
        FlowConfigurationStore.Save(_flows);
        UpdateListUiState();
    }

    private void UpdateListUiState()
    {
        var hasFlows = _flows.Count > 0;
        EmptyStateText.IsVisible = !hasFlows;
        FlowListScrollViewer.IsVisible = hasFlows;
        if (!hasFlows)
        {
            HidePopover();
            _selectedFlow = null;
            _hoveredFlow = null;
        }

        UpdateRowHighlights();
    }

    private void ShowPopover(FlowListItem flow, Control rowControl, Point anchorPoint, bool pinned)
    {
        _activePopoverFlow = flow;
        _isPopoverPinned = pinned;
        PopoverPinnedHintText.IsVisible = pinned;

        PopoverTitleText.Text = string.IsNullOrWhiteSpace(flow.FlowName) ? "Flow Details" : flow.FlowName;
        PopoverSourceText.Text = ToDisplayText(flow.Source);
        PopoverDestinationText.Text = ToDisplayText(flow.Destination);
        PopoverDataItemsText.Text = ToDisplayText(flow.DataItems);
        PopoverDataRulesText.Text = ToDisplayText(flow.DataRules);

        FlowPopoverCanvas.IsVisible = true;
        PositionPopover(rowControl, anchorPoint);
    }

    private void PositionPopover(Control rowControl, Point anchorPoint)
    {
        var transform = rowControl.TransformToVisual(FlowPopoverCanvas);
        if (transform is null)
        {
            return;
        }

        var rowOrigin = transform.Value.Transform(default);
        var rowRect = new Rect(rowOrigin, rowControl.Bounds.Size);

        FlowHoverPopover.Measure(new Size(FlowHoverPopover.Width, double.PositiveInfinity));
        var popupWidth = double.IsNaN(FlowHoverPopover.Width)
            ? Math.Max(FlowHoverPopover.DesiredSize.Width, 280)
            : FlowHoverPopover.Width;
        var popupHeight = Math.Max(FlowHoverPopover.DesiredSize.Height, 180);

        var canvasWidth = FlowPopoverCanvas.Bounds.Width;
        var canvasHeight = FlowPopoverCanvas.Bounds.Height;
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return;
        }

        var spacing = 12d;
        var minHorizontalOverlap = 22d;
        var minVerticalOverlap = Math.Clamp(rowRect.Height * 0.5, 14d, Math.Max(14d, rowRect.Height - 2d));

        var rightCandidate = anchorPoint.X + spacing;
        var leftCandidate = anchorPoint.X - spacing - popupWidth;
        var fitsRight = rightCandidate + popupWidth <= canvasWidth;
        var fitsLeft = leftCandidate >= 0;

        var left = rightCandidate;
        if (!fitsRight && fitsLeft)
        {
            left = leftCandidate;
        }
        else if (!fitsRight && !fitsLeft)
        {
            left = Math.Clamp(rightCandidate, 0, Math.Max(0, canvasWidth - popupWidth));
        }

        var downCandidate = anchorPoint.Y + spacing;
        var upCandidate = anchorPoint.Y - spacing - popupHeight;
        var opensDown = downCandidate + popupHeight <= canvasHeight;
        var top = opensDown ? downCandidate : upCandidate;

        // Keep the popover near the cursor but always overlapping the hovered row.
        var overlapLeft = rowRect.Right - minHorizontalOverlap;
        var overlapRight = rowRect.Left + minHorizontalOverlap;
        left = Math.Clamp(left, overlapRight - popupWidth, overlapLeft);

        var overlapTop = rowRect.Bottom - minVerticalOverlap;
        var overlapBottom = rowRect.Top + minVerticalOverlap;
        top = Math.Clamp(top, overlapBottom - popupHeight, overlapTop);

        left = Math.Clamp(left, 0, Math.Max(0, canvasWidth - popupWidth));
        top = Math.Clamp(top, 0, Math.Max(0, canvasHeight - popupHeight));

        Canvas.SetLeft(FlowHoverPopover, left);
        Canvas.SetTop(FlowHoverPopover, top);
    }

    private void UnpinPopover()
    {
        _isPopoverPinned = false;
        PopoverPinnedHintText.IsVisible = false;
        _isPointerInsidePopover = false;
        CancelHoverExitTimer();
        if (_hoveredFlow is null)
        {
            HidePopover();
        }
    }

    private void HidePopover()
    {
        _activePopoverFlow = null;
        _isPopoverPinned = false;
        _isPointerInsidePopover = false;
        PopoverPinnedHintText.IsVisible = false;
        FlowPopoverCanvas.IsVisible = false;
        CancelHoverExitTimer();
    }

    private static Control? GetFlowRowFromSource(object? source)
    {
        for (var current = source as ILogical; current is not null; current = current.LogicalParent)
        {
            if (current is Control control && control.DataContext is FlowListItem)
            {
                return control;
            }
        }

        return null;
    }

    private static string ToDisplayText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static bool IsInsideControl(object? source, Control target)
    {
        for (var current = source as ILogical; current is not null; current = current.LogicalParent)
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateRowHighlights()
    {
        var flowRows = FlowListItemsControl.GetVisualDescendants()
            .OfType<Border>()
            .Where(row => row.Classes.Contains("flow-row"));

        foreach (var row in flowRows)
        {
            if (row.DataContext is not FlowListItem flow)
            {
                row.Classes.Remove("hovered");
                row.Classes.Remove("selected");
                continue;
            }

            SetClass(row, "hovered", _hoveredFlow == flow);
            SetClass(row, "selected", _selectedFlow == flow);
        }
    }

    private static void SetClass(StyledElement element, string className, bool isEnabled)
    {
        if (isEnabled)
        {
            element.Classes.Add(className);
            return;
        }

        element.Classes.Remove(className);
    }

    private void OnHoverExitTimerTick(object? sender, EventArgs e)
    {
        _hoverExitTimer.Stop();
        if (!_isPopoverPinned && !_isPointerInsidePopover && _hoveredFlow is null)
        {
            HidePopover();
        }
    }

    private void CancelHoverExitTimer()
    {
        if (_hoverExitTimer.IsEnabled)
        {
            _hoverExitTimer.Stop();
        }
    }

    private void ShowMainListPage()
    {
        LoadSavedFlows();
        MainListPage.IsVisible = true;
        NewItemPage.IsVisible = false;
        UpdateWizardNavSelection(WizardSection.Flows);
    }

    private void ShowNewItemPage()
    {
        HidePopover();
        _hoveredFlow = null;
        _selectedFlow = null;
        UpdateRowHighlights();
        MainListPage.IsVisible = false;
        NewItemPage.IsVisible = true;
    }

    private void SelectWizardSection(WizardSection section, bool resetFlowWizard = false)
    {
        if (!NewItemPage.IsVisible)
        {
            ShowNewItemPage();
        }

        if (section == WizardSection.Flows && resetFlowWizard)
        {
            _flowsWizard.ResetForCreate();
        }

        WizardContentHost.Content = section switch
        {
            WizardSection.Flows => _flowsWizard,
            WizardSection.Connections => _connectionsWizard,
            WizardSection.DataItems => _dataItemsWizard,
            _ => _dataRulesWizard
        };

        UpdateWizardNavSelection(section);
    }

    private void UpdateWizardNavSelection(WizardSection? section)
    {
        var isMainPageVisible = MainListPage.IsVisible;

        SetClass(MainNavMainListButton, "selected", section is null && isMainPageVisible);
        SetClass(MainNavFlowsButton, "selected", section == WizardSection.Flows);

        SetClass(NavMainListButton, "selected", section is null && !isMainPageVisible);
        SetClass(NavFlowsButton, "selected", section == WizardSection.Flows);
        SetClass(NavConnectionsButton, "selected", section == WizardSection.Connections);
        SetClass(NavDataItemsButton, "selected", section == WizardSection.DataItems);
        SetClass(NavDataRulesButton, "selected", section == WizardSection.DataRules);
    }
}