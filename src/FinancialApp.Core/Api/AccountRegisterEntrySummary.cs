namespace FinancialApp.Core.Api;

public sealed record AccountRegisterEntrySummary(
    Guid JournalEntryId,
    DateOnly EntryDate,
    string? Memo,
    decimal Debit,
    decimal Credit,
    string OtherAccounts);
