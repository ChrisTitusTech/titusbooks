namespace FinancialApp.Importers;

public sealed record CsvImportResult(
    Guid? ImportBatchId,
    int PendingCount,
    int CategorizedCount,
    int DuplicateCount,
    int ErrorCount,
    int SkippedCount = 0);
