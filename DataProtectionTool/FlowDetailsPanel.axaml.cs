using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace DataProtectionTool;

public partial class FlowDetailsPanel : UserControl
{
    public event EventHandler<FlowDetailsInput>? SaveRequested;

    public FlowDetailsPanel()
    {
        InitializeComponent();
    }

    public void SetCreationMode()
    {
        PanelTitleText.Text = "New Flow";
        FlowNameTextBox.Text = string.Empty;
        SourceConnectionTextBox.Text = string.Empty;
        DataItemsTextBox.Text = string.Empty;
        DataRulesTextBox.Text = string.Empty;
        DestinationTextBox.Text = string.Empty;
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var details = new FlowDetailsInput
        {
            FlowName = FlowNameTextBox.Text?.Trim() ?? string.Empty,
            SourceConnection = SourceConnectionTextBox.Text?.Trim() ?? string.Empty,
            DataItems = DataItemsTextBox.Text?.Trim() ?? string.Empty,
            DataRules = DataRulesTextBox.Text?.Trim() ?? string.Empty,
            Destination = DestinationTextBox.Text?.Trim() ?? string.Empty
        };

        SaveRequested?.Invoke(this, details);
    }
}
