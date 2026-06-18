namespace FinancialApp.Core.Api;

public sealed record ImportedTransactionSummary(
    Guid Id,
    Guid? ImportBatchId,
    string Source,
    DateOnly PostedDate,
    string Description,
    decimal Amount,
    string Currency,
    string Status);
