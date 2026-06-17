namespace FinancialApp.Core.Accounting;

public interface IJournalEntryRepository
{
    Task AddAsync(JournalEntry journalEntry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountRegisterEntry>> ListRegisterAsync(
        Guid organizationId,
        Guid accountId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default);
}
