using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using FinancialApp.Core.Api;
using FinancialApp.Core.Application;
using FinancialApp.Desktop.ViewModels;
using FinancialApp.Desktop.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinancialApp.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configuration = BuildConfiguration();
            var settings = configuration.Get<AppSettings>() ?? new AppSettings();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConfiguration(configuration.GetSection("Logging"))
                    .AddConsole();
            });

            loggerFactory
                .CreateLogger<App>()
                .LogInformation("{ApplicationName} starting with {EnvironmentName} settings.", settings.ApplicationName, settings.EnvironmentName);

            var viewModel = new MainWindowViewModel(settings);
            _ = CheckApiHealthAsync(settings, viewModel);

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task CheckApiHealthAsync(AppSettings settings, MainWindowViewModel viewModel)
    {
        if (!Uri.TryCreate(settings.Api.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return;
        }

        using var httpClient = new HttpClient { BaseAddress = baseUri };
        var apiHealthClient = new ApiHealthClient(httpClient);
        await viewModel.CheckApiHealthAsync(apiHealthClient);
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
            .Build();
    }
}
