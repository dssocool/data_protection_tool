using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace DataProtectionTool;

public partial class FlowsWizard : UserControl
{
    private int _stepIndex;

    public event EventHandler<FlowDetailsInput>? SaveRequested;

    public FlowsWizard()
    {
        InitializeComponent();
        ResetForCreate();
    }

    public void ResetForCreate()
    {
        _stepIndex = 0;
        FlowNameTextBox.Text = string.Empty;
        SourceConnectionTextBox.Text = string.Empty;
        DestinationTextBox.Text = string.Empty;
        DataItemsTextBox.Text = string.Empty;
        DataRulesTextBox.Text = string.Empty;
        UpdateStepUi();
    }

    private void OnBackStepClicked(object? sender, RoutedEventArgs e)
    {
        if (_stepIndex == 0)
        {
            return;
        }

        _stepIndex--;
        UpdateStepUi();
    }

    private void OnNextStepClicked(object? sender, RoutedEventArgs e)
    {
        if (_stepIndex >= 1)
        {
            return;
        }

        _stepIndex++;
        UpdateStepUi();
    }

    private void OnSaveFlowClicked(object? sender, RoutedEventArgs e)
    {
        var details = new FlowDetailsInput
        {
            FlowName = FlowNameTextBox.Text?.Trim() ?? string.Empty,
            SourceConnection = SourceConnectionTextBox.Text?.Trim() ?? string.Empty,
            Destination = DestinationTextBox.Text?.Trim() ?? string.Empty,
            DataItems = DataItemsTextBox.Text?.Trim() ?? string.Empty,
            DataRules = DataRulesTextBox.Text?.Trim() ?? string.Empty
        };

        SaveRequested?.Invoke(this, details);
    }

    private void UpdateStepUi()
    {
        var isStepOne = _stepIndex == 0;
        StepOnePanel.IsVisible = isStepOne;
        StepTwoPanel.IsVisible = !isStepOne;
        BackStepButton.IsEnabled = !isStepOne;
        NextStepButton.IsVisible = isStepOne;
        SaveFlowButton.IsVisible = !isStepOne;
        StepDescriptionText.Text = isStepOne
            ? "Step 1 of 2 - Basic flow setup"
            : "Step 2 of 2 - Data settings";
    }
}
