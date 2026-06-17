using FinancialApp.Core.Accounting;

namespace FinancialApp.Api.Accounting;

public sealed record AccountRegisterEntryResponse(
    Guid JournalEntryId,
    DateOnly EntryDate,
    string? Memo,
    decimal Debit,
    decimal Credit,
    string OtherAccounts)
{
    public static AccountRegisterEntryResponse FromRegisterEntry(AccountRegisterEntry entry)
    {
        return new AccountRegisterEntryResponse(
            entry.JournalEntryId,
            entry.EntryDate,
            entry.Memo,
            entry.Debit,
            entry.Credit,
            entry.OtherAccounts);
    }
}
