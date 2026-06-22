namespace FinancialApp.Core.Reconciliation;

public sealed record ReconciliationPreview(
    Guid AccountId,
    DateOnly StatementEndDate,
    decimal StatementEndBalance,
    decimal ClearedBalance,
    decimal Difference,
    IReadOnlyList<ReconciliationTransaction> Transactions);
