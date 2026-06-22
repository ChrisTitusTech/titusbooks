namespace FinancialApp.Api.Reconciliation;

public sealed record PreviewReconciliationRequest(
    DateOnly StatementEndDate,
    decimal StatementEndBalance,
    IReadOnlyList<Guid> ClearedJournalLineIds);
