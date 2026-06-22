namespace FinancialApp.Api.Reconciliation;

public sealed record CompleteReconciliationRequest(
    DateOnly StatementEndDate,
    decimal StatementEndBalance,
    IReadOnlyList<Guid> ClearedJournalLineIds);
