using FinancialApp.Core.Imports;
using FinancialApp.Importers;

namespace FinancialApp.Importers.Tests;

public sealed class CsvImportServiceTests
{
    [Fact]
    public async Task ImportAsync_SkipsDuplicatesWithinFileAndAcrossImports()
    {
        const string csv = """
            Date,Description,Amount
            2026-06-01,Same row,-10.00
            2026-06-01,Same row,-10.00
            2026-06-02,Unique row,20.00
            """;
        var repository = new InMemoryImportRepository();
        var service = new CsvImportService(new GenericCsvParser(), repository);
        var request = new CsvImportRequest(
            Guid.NewGuid(),
            "Generic CSV",
            "fake.csv",
            csv,
            new CsvColumnMapping("Date", "Description", AmountColumn: "Amount"));

        var first = await service.ImportAsync(request);
        var second = await service.ImportAsync(request);

        Assert.Equal(2, first.PendingCount);
        Assert.Equal(1, first.DuplicateCount);
        Assert.Equal(0, first.ErrorCount);
        Assert.Equal(0, second.PendingCount);
        Assert.Equal(3, second.DuplicateCount);
        Assert.Equal(2, repository.Transactions.Count);
        Assert.All(repository.Transactions, transaction =>
            Assert.Equal(ImportedTransactionStatus.Pending, transaction.Status));
    }

    [Fact]
    public async Task ImportAsync_IsolatesInvalidRows()
    {
        const string csv = """
            Date,Description,Amount
            2026-06-01,Valid,-10.00
            invalid,Bad,5.00
            """;
        var repository = new InMemoryImportRepository();
        var service = new CsvImportService(new GenericCsvParser(), repository);
        var request = new CsvImportRequest(
            Guid.NewGuid(),
            "Generic CSV",
            "fake.csv",
            csv,
            new CsvColumnMapping("Date", "Description", AmountColumn: "Amount"));

        var result = await service.ImportAsync(request);

        Assert.Equal(1, result.PendingCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Single(repository.Transactions);
    }

    private sealed class InMemoryImportRepository : IImportRepository
    {
        public List<ImportedTransaction> Transactions { get; } = [];

        public Task<IReadOnlySet<string>> FindExistingFingerprintsAsync(
            Guid organizationId,
            string source,
            IReadOnlyCollection<string> fingerprints,
            CancellationToken cancellationToken = default)
        {
            IReadOnlySet<string> existing = Transactions
                .Where(transaction =>
                    transaction.OrganizationId == organizationId
                    && transaction.Source == source
                    && fingerprints.Contains(transaction.Fingerprint))
                .Select(transaction => transaction.Fingerprint)
                .ToHashSet(StringComparer.Ordinal);
            return Task.FromResult(existing);
        }

        public Task AddBatchAsync(
            ImportBatch batch,
            IReadOnlyCollection<ImportedTransaction> transactions,
            CancellationToken cancellationToken = default)
        {
            Transactions.AddRange(transactions);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ImportedTransaction>> ListTransactionsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ImportedTransaction>>(
                Transactions.Where(transaction => transaction.OrganizationId == organizationId).ToList());
        }
    }
}
