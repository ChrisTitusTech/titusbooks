using FinancialApp.Core.Application;

namespace FinancialApp.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
        : this(new AppSettings())
    {
    }

    public MainWindowViewModel(AppSettings settings)
    {
        Title = settings.ApplicationName;
        StatusMessage = "Phase 0 foundation is ready.";
        DatabaseSummary = $"PostgreSQL target: {settings.Database.Host}:{settings.Database.Port}/{settings.Database.DatabaseName}";
    }

    public string Title { get; }

    public string StatusMessage { get; }

    public string DatabaseSummary { get; }
}
