namespace FinancialApp.Core.Accounting;

public sealed record JournalEntryDraft
{
    public required Guid OrganizationId { get; init; }

    public required DateOnly EntryDate { get; init; }

    public string? Memo { get; init; }

    public Guid? SourceImportedTransactionId { get; init; }

    public required IReadOnlyList<JournalLineDraft> Lines { get; init; }

    public JournalEntry ToJournalEntry()
    {
        var journalEntryId = Guid.NewGuid();

        return new JournalEntry
        {
            Id = journalEntryId,
            OrganizationId = OrganizationId,
            EntryDate = EntryDate,
            Memo = Memo,
            SourceImportedTransactionId = SourceImportedTransactionId,
            Lines = Lines.Select(line => line.ToJournalLine(journalEntryId)).ToList()
        };
    }
}
