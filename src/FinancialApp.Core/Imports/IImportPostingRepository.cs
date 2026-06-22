using FinancialApp.Core.Accounting;

namespace FinancialApp.Core.Imports;

public interface IImportPostingRepository
{
    Task<IReadOnlyList<ImportedTransaction>> ListForPostingAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> transactionIds,
        CancellationToken cancellationToken = default);

    Task PostAsync(
        Guid organizationId,
        IReadOnlyCollection<JournalEntry> journalEntries,
        IReadOnlyDictionary<Guid, Guid> expectedCategoryAccountIds,
        CancellationToken cancellationToken = default);
}
