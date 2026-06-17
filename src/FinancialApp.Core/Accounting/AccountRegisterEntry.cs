namespace FinancialApp.Core.Accounting;

public sealed record AccountRegisterEntry(
    Guid JournalEntryId,
    DateOnly EntryDate,
    string? Memo,
    decimal Debit,
    decimal Credit,
    string OtherAccounts);
