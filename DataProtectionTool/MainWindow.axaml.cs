using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DataProtectionTool;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnNewFlowClicked(object? sender, RoutedEventArgs e)
    {
        RootLayoutGrid.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
        RootLayoutGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
        DetailsPanelHost.IsVisible = true;
    }
}