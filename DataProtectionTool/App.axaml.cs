using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DataProtectionTool.Services.Abstractions;
using DataProtectionTool.Services;
using DataProtectionTool.Views;

namespace DataProtectionTool;

public partial class App : Application
{
    private IDelphixApiService? _delphixApiService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppStorage.EnsureConfigDirectoryExists();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _delphixApiService ??= DelphixApiServiceResolver.CreateForCurrentMode();
            desktop.MainWindow = new MainWindow(_delphixApiService);
        }

        base.OnFrameworkInitializationCompleted();
    }
}