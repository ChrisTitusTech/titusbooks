namespace FinancialApp.Desktop.Services;

public interface IImportFilePicker
{
    Task<ImportFileContent?> PickCsvAsync(CancellationToken cancellationToken = default);
}
