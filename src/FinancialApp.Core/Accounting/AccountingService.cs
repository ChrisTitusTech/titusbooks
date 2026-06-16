namespace FinancialApp.Core.Accounting;

public sealed class AccountingService
{
    private readonly IJournalEntryRepository journalEntryRepository;

    public AccountingService(IJournalEntryRepository journalEntryRepository)
    {
        this.journalEntryRepository = journalEntryRepository;
    }

    public async Task<JournalEntry> PostJournalEntryAsync(
        JournalEntryDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var entry = draft.ToJournalEntry();
        entry.EnsureBalanced();

        await journalEntryRepository.AddAsync(entry, cancellationToken);
        return entry;
    }
}
