namespace FinancialApp.Importers;

public sealed record CsvImportResult(
    Guid? ImportBatchId,
    int PendingCount,
    int DuplicateCount,
    int ErrorCount,
    int SkippedCount = 0);
