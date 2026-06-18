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
        Assert.NotNull(first.ImportBatchId);
        Assert.Null(second.ImportBatchId);
        Assert.Single(repository.Batches);
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

    [Fact]
    public async Task ImportAsync_DoesNotCreateBatchWhenEveryRowIsInvalid()
    {
        const string csv = """
            Date,Description,Amount
            invalid,Bad date,5.00
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

        Assert.Null(result.ImportBatchId);
        Assert.Equal(0, result.PendingCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Empty(repository.Batches);
        Assert.Empty(repository.Transactions);
    }

    [Fact]
    public async Task ImportAsync_DetectsDuplicatesWhenSourceCasingChanges()
    {
        const string csv = """
            Date,Description,Amount
            2026-06-01,Same row,-10.00
            """;
        var organizationId = Guid.NewGuid();
        var repository = new InMemoryImportRepository();
        var service = new CsvImportService(new GenericCsvParser(), repository);
        var mapping = new CsvColumnMapping("Date", "Description", AmountColumn: "Amount");

        var first = await service.ImportAsync(
            new CsvImportRequest(organizationId, "Generic CSV", "first.csv", csv, mapping));
        var second = await service.ImportAsync(
            new CsvImportRequest(organizationId, "generic csv", "second.csv", csv, mapping));

        Assert.Equal(1, first.PendingCount);
        Assert.Equal(0, second.PendingCount);
        Assert.Equal(1, second.DuplicateCount);
        Assert.Single(repository.Transactions);
    }

    private sealed class InMemoryImportRepository : IImportRepository
    {
        public List<ImportBatch> Batches { get; } = [];

        public List<ImportedTransaction> Transactions { get; } = [];

        public Task<IReadOnlySet<string>> FindExistingFingerprintsAsync(
            Guid organizationId,
            IReadOnlyCollection<string> fingerprints,
            CancellationToken cancellationToken = default)
        {
            IReadOnlySet<string> existing = Transactions
                .Where(transaction =>
                    transaction.OrganizationId == organizationId
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
            Batches.Add(batch);
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
