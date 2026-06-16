namespace FinancialApp.Core.Accounting;

public interface IJournalEntryRepository
{
    Task AddAsync(JournalEntry journalEntry, CancellationToken cancellationToken = default);
}
