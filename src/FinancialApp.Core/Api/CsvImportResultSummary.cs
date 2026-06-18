namespace FinancialApp.Core.Api;

public sealed record CsvImportResultSummary(
    Guid? ImportBatchId,
    int PendingCount,
    int DuplicateCount,
    int ErrorCount);
