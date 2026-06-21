namespace FinancialApp.Core.Api;

public sealed record CsvImportResultSummary(
    Guid? ImportBatchId,
    int PendingCount,
    int CategorizedCount,
    int DuplicateCount,
    int ErrorCount,
    int SkippedCount);
