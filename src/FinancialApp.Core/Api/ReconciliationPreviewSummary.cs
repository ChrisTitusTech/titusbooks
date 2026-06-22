namespace FinancialApp.Core.Api;

public sealed record ReconciliationPreviewSummary(
    Guid AccountId,
    DateOnly StatementEndDate,
    decimal StatementEndBalance,
    decimal ClearedBalance,
    decimal Difference,
    IReadOnlyList<ReconciliationTransactionSummary> Transactions);
