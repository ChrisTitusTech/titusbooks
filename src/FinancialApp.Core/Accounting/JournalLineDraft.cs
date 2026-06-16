namespace FinancialApp.Core.Accounting;

public sealed record JournalLineDraft
{
    public required Guid AccountId { get; init; }

    public decimal Debit { get; init; }

    public decimal Credit { get; init; }

    public string? Memo { get; init; }

    public JournalLine ToJournalLine(Guid journalEntryId)
    {
        return new JournalLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = journalEntryId,
            AccountId = AccountId,
            Debit = Debit,
            Credit = Credit,
            Memo = Memo
        };
    }
}
