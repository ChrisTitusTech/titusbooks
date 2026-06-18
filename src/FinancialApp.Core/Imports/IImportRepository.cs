namespace FinancialApp.Core.Imports;

public interface IImportRepository
{
    Task<IReadOnlySet<string>> FindExistingFingerprintsAsync(
        Guid organizationId,
        string source,
        IReadOnlyCollection<string> fingerprints,
        CancellationToken cancellationToken = default);

    Task AddBatchAsync(
        ImportBatch batch,
        IReadOnlyCollection<ImportedTransaction> transactions,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportedTransaction>> ListTransactionsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);
}
