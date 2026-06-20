using FinancialApp.Core.Imports;
using FinancialApp.Importers;

namespace FinancialApp.Importers.Tests;

public sealed class BankOfAmericaImportProfileTests
{
    [Fact]
    public void GenericProfile_AllowsUnknownHeadersForManualMapping()
    {
        var mapping = CsvImportProfiles
            .Get(CsvImportProfiles.GenericCsvName)
            .CreateMapping(["When", "Who", "How Much"]);

        Assert.Empty(mapping.DateColumn);
        Assert.Empty(mapping.DescriptionColumn);
        Assert.Null(mapping.AmountColumn);
    }

    [Fact]
    public void Profile_MapsAndParsesBankOfAmericaFixtureWithoutManualMapping()
    {
        var csv = ReadFixture();
        var parser = new GenericCsvParser();
        var headers = parser.ReadHeaders(csv);
        var profile = CsvImportProfiles.Get(CsvImportProfiles.BankOfAmericaName);
        var mapping = profile.CreateMapping(headers);
        var request = CreateRequest(csv, mapping);

        var preview = parser.Parse(request);

        Assert.Equal("Posted Date", mapping.DateColumn);
        Assert.Equal("Payee", mapping.DescriptionColumn);
        Assert.Equal("Amount", mapping.AmountColumn);
        Assert.Equal("Reference Number", mapping.SourceTransactionIdColumn);
        Assert.Equal("Running Bal.", mapping.BalanceColumn);
        Assert.Equal(3, preview.ValidCount);
        Assert.Equal(1, preview.ErrorCount);
        Assert.Equal(-5.25m, preview.Rows[0].Transaction!.Amount);
        Assert.Equal(1000m, preview.Rows[0].Transaction!.Balance);
        Assert.Equal(1250m, preview.Rows[1].Transaction!.Amount);
        Assert.Equal(-42.10m, preview.Rows[2].Transaction!.Amount);
        Assert.Contains("Invalid date", preview.Rows[3].Error);
    }

    [Fact]
    public void Profile_SkipsBankOfAmericaSummaryPreamble()
    {
        const string csv = """
            Description,,Summary Amt.
            Beginning balance,,1000.00
            Deposits and other additions,,250.00

            Date,Description,Amount,Running Bal.
            01/01/2026,Beginning balance as of 01/01/2026,,1000.00
            06/01/2026,Coffee Shop,-5.25,1244.75
            """;
        var parser = new GenericCsvParser();
        var headers = parser.ReadHeaders(csv);
        var mapping = CsvImportProfiles
            .Get(CsvImportProfiles.BankOfAmericaName)
            .CreateMapping(headers);

        var preview = parser.Parse(CreateRequest(csv, mapping));

        Assert.Equal(["Date", "Description", "Amount", "Running Bal."], headers);
        var row = Assert.Single(preview.Rows);
        Assert.Equal(6, row.RowNumber);
        Assert.Equal(-5.25m, row.Transaction!.Amount);
        Assert.Equal(1244.75m, row.Transaction.Balance);
    }

    [Fact]
    public async Task RepeatedBankOfAmericaImport_ReportsPendingDuplicatesAndErrors()
    {
        var csv = ReadFixture();
        var parser = new GenericCsvParser();
        var mapping = CsvImportProfiles
            .Get(CsvImportProfiles.BankOfAmericaName)
            .CreateMapping(parser.ReadHeaders(csv));
        var repository = new InMemoryImportRepository();
        var service = new CsvImportService(parser, repository);
        var request = CreateRequest(csv, mapping);

        var first = await service.ImportAsync(request);
        var second = await service.ImportAsync(request);

        Assert.Equal(3, first.PendingCount);
        Assert.Equal(0, first.DuplicateCount);
        Assert.Equal(1, first.ErrorCount);
        Assert.Equal(0, second.PendingCount);
        Assert.Equal(3, second.DuplicateCount);
        Assert.Equal(1, second.ErrorCount);
    }

    private static string ReadFixture()
    {
        return File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "bank-of-america.csv"));
    }

    private static CsvImportRequest CreateRequest(string csv, CsvColumnMapping mapping)
    {
        return new CsvImportRequest(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            CsvImportProfiles.BankOfAmericaName,
            "bank-of-america.csv",
            csv,
            mapping);
    }

    private sealed class InMemoryImportRepository : IImportRepository
    {
        private readonly List<ImportedTransaction> transactions = [];

        public Task<IReadOnlySet<string>> FindExistingFingerprintsAsync(
            Guid organizationId,
            IReadOnlyCollection<string> fingerprints,
            CancellationToken cancellationToken = default)
        {
            IReadOnlySet<string> existing = transactions
                .Where(transaction =>
                    transaction.OrganizationId == organizationId
                    && fingerprints.Contains(transaction.Fingerprint))
                .Select(transaction => transaction.Fingerprint)
                .ToHashSet(StringComparer.Ordinal);
            return Task.FromResult(existing);
        }

        public Task AddBatchAsync(
            ImportBatch batch,
            IReadOnlyCollection<ImportedTransaction> importedTransactions,
            CancellationToken cancellationToken = default)
        {
            transactions.AddRange(importedTransactions);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ImportedTransaction>> ListTransactionsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ImportedTransaction>>(
                transactions.Where(transaction =>
                    transaction.OrganizationId == organizationId).ToList());
        }
    }
}
