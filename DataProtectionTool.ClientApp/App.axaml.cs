using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DataProtectionTool.ClientApp.Services;
using DataProtectionTool.ClientApp.Views;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DataProtectionTool.ClientApp;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppStorage.EnsureConfigDirectoryExists();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await MasterGrpcClient.TryPingAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Master] Ping failed (is DataProtectionTool.Master running?): {ex.Message}");
            }
        });

        base.OnFrameworkInitializationCompleted();
    }
}