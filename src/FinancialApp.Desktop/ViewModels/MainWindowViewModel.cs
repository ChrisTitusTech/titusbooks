using CommunityToolkit.Mvvm.ComponentModel;
using FinancialApp.Core.Api;
using FinancialApp.Core.Application;

namespace FinancialApp.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string apiHealthMessage = "API health has not been checked.";

    public MainWindowViewModel()
        : this(new AppSettings())
    {
    }

    public MainWindowViewModel(AppSettings settings)
    {
        Title = settings.ApplicationName;
        StatusMessage = "Checking API connection...";
        DatabaseSummary = $"API target: {settings.Api.BaseUrl}";
    }

    public string Title { get; }

    public string StatusMessage { get; }

    public string DatabaseSummary { get; }

    public async Task CheckApiHealthAsync(ApiHealthClient apiHealthClient, CancellationToken cancellationToken = default)
    {
        var health = await apiHealthClient.CheckHealthAsync(cancellationToken);
        ApiHealthMessage = health.Message;
    }
}
