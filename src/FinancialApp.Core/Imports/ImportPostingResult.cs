namespace FinancialApp.Core.Imports;

public sealed record ImportPostingResult(
    int PostedCount,
    IReadOnlyList<Guid> JournalEntryIds);
