namespace FinancialApp.Core.Api;

public sealed record ReconciliationCommand(
    DateOnly StatementEndDate,
    decimal StatementEndBalance,
    IReadOnlyList<Guid> ClearedJournalLineIds);
