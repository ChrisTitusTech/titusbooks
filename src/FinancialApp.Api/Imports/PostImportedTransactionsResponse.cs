namespace FinancialApp.Api.Imports;

public sealed record PostImportedTransactionsResponse(
    int PostedCount,
    IReadOnlyList<Guid> JournalEntryIds);
