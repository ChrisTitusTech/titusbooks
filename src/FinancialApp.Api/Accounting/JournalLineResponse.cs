using FinancialApp.Core.Accounting;

namespace FinancialApp.Api.Accounting;

public sealed record JournalLineResponse(
    Guid Id,
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string? Memo)
{
    public static JournalLineResponse FromJournalLine(JournalLine line)
    {
        return new JournalLineResponse(
            line.Id,
            line.AccountId,
            line.Debit,
            line.Credit,
            line.Memo);
    }
}
