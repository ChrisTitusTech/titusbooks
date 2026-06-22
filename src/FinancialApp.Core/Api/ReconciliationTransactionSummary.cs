namespace FinancialApp.Core.Api;

public sealed record ReconciliationTransactionSummary(
    Guid JournalLineId,
    Guid JournalEntryId,
    DateOnly EntryDate,
    string? Memo,
    decimal Debit,
    decimal Credit,
    string OtherAccounts,
    bool IsReconciled);
