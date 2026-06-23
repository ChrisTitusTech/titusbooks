using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using FinancialApp.Core.Api;
using FinancialApp.Core.Application;
using FinancialApp.Desktop.Services;
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
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var userSettingsPath = GetUserSettingsPath();
            var configuration = BuildConfiguration(userSettingsPath);
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

            MainWindow? mainWindow = null;
            var apiHttpClient = CreateApiHttpClient(settings);
            var reportFileSaver = new AvaloniaReportFileSaver(() => mainWindow?.StorageProvider);
            var importFilePicker = new AvaloniaImportFilePicker(() => mainWindow?.StorageProvider);
            var viewModel = new MainWindowViewModel(
                settings,
                new TitusBooksApiClient(apiHttpClient),
                reportFileSaver,
                importFilePicker,
                new UserSettingsStore(userSettingsPath));
            _ = CheckApiHealthAsync(apiHttpClient, viewModel);
            _ = viewModel.InitializeAsync();

            mainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static HttpClient CreateApiHttpClient(AppSettings settings)
    {
        try
        {
            var baseUri = ApiEndpointValidator.Validate(
                settings.Api.BaseUrl,
                settings.Api.AllowInsecureRemoteHttp);
            var timeoutSeconds = Math.Clamp(settings.Api.RequestTimeoutSeconds, 5, 300);

            return new HttpClient
            {
                BaseAddress = baseUri,
                Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            };
        }
        catch (ArgumentException)
        {
            return new HttpClient();
        }
    }

    private static async Task CheckApiHealthAsync(HttpClient httpClient, MainWindowViewModel viewModel)
    {
        var apiHealthClient = new ApiHealthClient(httpClient);
        await viewModel.CheckApiHealthAsync(apiHealthClient);
    }

    private static IConfiguration BuildConfiguration(string userSettingsPath)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var applicationDirectory = AppContext.BaseDirectory;
        var projectLocalSettings = Path.Combine(currentDirectory, "src", "FinancialApp.Desktop", "appsettings.Local.json");

        return new ConfigurationBuilder()
            .SetBasePath(applicationDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine(currentDirectory, "appsettings.json"), optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine(currentDirectory, "appsettings.Local.json"), optional: true, reloadOnChange: true)
            .AddJsonFile(projectLocalSettings, optional: true, reloadOnChange: true)
            .AddJsonFile(userSettingsPath, optional: true, reloadOnChange: true)
            .Build();
    }

    private static string GetUserSettingsPath()
    {
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(applicationData, "TitusBooks", "settings.json");
    }
}
