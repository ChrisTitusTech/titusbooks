namespace FinancialApp.Core.Api;

public sealed record CsvImportPreviewSummary(
    IReadOnlyList<string> Headers,
    IReadOnlyList<CsvImportPreviewRowSummary> Rows,
    int ValidCount,
    int ErrorCount,
    int SkippedCount);
