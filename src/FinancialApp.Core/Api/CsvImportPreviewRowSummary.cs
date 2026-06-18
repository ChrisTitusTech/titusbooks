namespace FinancialApp.Core.Api;

public sealed record CsvImportPreviewRowSummary(
    int RowNumber,
    DateOnly? PostedDate,
    string? Description,
    decimal? Amount,
    string? Currency,
    string? Error);
