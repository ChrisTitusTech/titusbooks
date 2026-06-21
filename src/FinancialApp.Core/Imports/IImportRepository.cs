namespace FinancialApp.Core.Imports;

public interface IImportRepository
{
    Task<IReadOnlySet<string>> FindExistingFingerprintsAsync(
        Guid organizationId,
        IReadOnlyCollection<string> fingerprints,
        CancellationToken cancellationToken = default);

    Task AddBatchAsync(
        ImportBatch batch,
        IReadOnlyCollection<ImportedTransaction> transactions,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportedTransaction>> ListTransactionsAsync(
        Guid organizationId,
        ImportedTransactionStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<bool> CategorizeTransactionsAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> transactionIds,
        Guid categoryAccountId,
        CancellationToken cancellationToken = default);
}
