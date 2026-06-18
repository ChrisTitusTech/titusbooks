namespace FinancialApp.Desktop.Services;

public interface IReportFileSaver
{
    Task<bool> SaveAsync(
        string suggestedFileName,
        string content,
        CancellationToken cancellationToken = default);
}
