using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using DataProtectionTool.Views;
using Xunit;

namespace DataProtectionTool.Tests;

public class MainWindowModeLabelsTests
{
    [AvaloniaFact]
    public void ModeLabels_AppearInCorrectWorkspaces()
    {
        var window = new MainWindow();
        window.Show();

        var mainListPage = window.FindControl<Grid>("MainListPage");
        var newItemPage = window.FindControl<Grid>("NewItemPage");
        var executionModeButton = window.FindControl<Button>("MainNavMainListButton");
        var configurationModeButton = window.FindControl<Button>("NavMainListButton");

        Assert.NotNull(mainListPage);
        Assert.NotNull(newItemPage);
        Assert.NotNull(executionModeButton);
        Assert.NotNull(configurationModeButton);

        Assert.True(mainListPage!.IsVisible);
        Assert.False(newItemPage!.IsVisible);
        Assert.Equal("Edit", Assert.IsType<TextBlock>(executionModeButton!.Content).Text);

        executionModeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.False(mainListPage.IsVisible);
        Assert.True(newItemPage.IsVisible);
        Assert.Equal("Run", Assert.IsType<TextBlock>(configurationModeButton!.Content).Text);

        window.Close();
    }
}
