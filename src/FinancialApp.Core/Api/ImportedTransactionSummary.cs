namespace FinancialApp.Core.Api;

public sealed record ImportedTransactionSummary(
    Guid Id,
    Guid? ImportBatchId,
    string Source,
    string? SourceType,
    string? SourceStatus,
    string Kind,
    DateOnly PostedDate,
    string Description,
    decimal Amount,
    decimal? Balance,
    decimal? GrossAmount,
    decimal? FeeAmount,
    decimal? NetAmount,
    string Currency,
    string Status);
