namespace FinancialApp.Core.Api;

public sealed record PostImportedTransactionsResult(
    int PostedCount,
    IReadOnlyList<Guid> JournalEntryIds);
