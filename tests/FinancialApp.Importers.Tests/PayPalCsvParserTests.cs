using FinancialApp.Core.Imports;
using FinancialApp.Importers;

namespace FinancialApp.Importers.Tests;

public sealed class PayPalCsvParserTests
{
    [Fact]
    public void Parse_NormalizesCompletedPaymentsRefundsAndTransfers()
    {
        var preview = CreateParser().Parse(CreateRequest(ReadFixture()));

        Assert.Equal(3, preview.ValidCount);
        Assert.Equal(1, preview.ErrorCount);
        Assert.Equal(3, preview.SkippedCount);

        var sale = preview.Rows.Single(row =>
            row.Transaction?.SourceTransactionId == "PAY-001").Transaction!;
        Assert.Equal(ImportedTransactionKind.Payment, sale.Kind);
        Assert.Equal(100m, sale.GrossAmount);
        Assert.Equal(3.49m, sale.FeeAmount);
        Assert.Equal(96.51m, sale.NetAmount);
        Assert.Equal(new TimeOnly(10, 15, 30), sale.PostedTime);

        var refund = preview.Rows.Single(row =>
            row.Transaction?.SourceTransactionId == "REF-001").Transaction!;
        Assert.Equal(ImportedTransactionKind.Refund, refund.Kind);
        Assert.Equal("PAY-001", refund.ReferenceTransactionId);

        var transfer = preview.Rows.Single(row =>
            row.Transaction?.SourceTransactionId == "TRN-001").Transaction!;
        Assert.Equal(ImportedTransactionKind.Transfer, transfer.Kind);
        Assert.Equal("Bank Transfer to Bank", transfer.Description);

        Assert.DoesNotContain(preview.Rows, row =>
            row.Transaction?.SourceTransactionId is "PENDING-001" or "MEMO-001" or "HOLD-001");
        Assert.Contains(preview.Rows, row =>
            row.Error == "PayPal gross plus fee must equal net.");
    }

    [Fact]
    public async Task Import_DetectsRepeatedPayPalTransactions()
    {
        var repository = new InMemoryImportRepository();
        var service = new CsvImportService(
            new GenericCsvParser(),
            repository,
            CreateParser());
        var request = CreateRequest(ReadFixture());

        var first = await service.ImportAsync(request);
        var second = await service.ImportAsync(request);

        Assert.Equal(3, first.PendingCount);
        Assert.Equal(0, first.DuplicateCount);
        Assert.Equal(1, first.ErrorCount);
        Assert.Equal(3, first.SkippedCount);
        Assert.Equal(0, second.PendingCount);
        Assert.Equal(3, second.DuplicateCount);
        Assert.Equal(1, second.ErrorCount);
        Assert.Equal(3, second.SkippedCount);
    }

    [Fact]
    public void Parse_RejectsMissingRequiredRowValues()
    {
        const string csv = """
            Date,Time,TimeZone,Name,Type,Status,Currency,Gross,Fee,Net,Transaction ID,Reference Txn ID,Balance Impact
            06/01/2026,10:15:30,CDT,Fake Customer,Express Checkout Payment,,USD,100.00,-3.49,96.51,PAY-001,,Credit
            """;

        var preview = CreateParser().Parse(CreateRequest(csv));

        var row = Assert.Single(preview.Rows);
        Assert.Equal("PayPal column 'Status' is required.", row.Error);
    }

    [Fact]
    public void Parse_StagesBalanceAffectingReversedRows()
    {
        const string csv = """
            Date,Time,TimeZone,Name,Type,Status,Currency,Gross,Fee,Net,Transaction ID,Reference Txn ID,Balance Impact
            06/01/2026,10:15:30,CDT,Fake Customer,Payment Reversal,Reversed,USD,-25.00,0.00,-25.00,REV-001,PAY-001,Debit
            """;

        var preview = CreateParser().Parse(CreateRequest(csv));

        var row = Assert.Single(preview.Rows);
        Assert.Null(row.Error);
        Assert.Equal("Reversed", row.Transaction!.SourceStatus);
        Assert.Equal(ImportedTransactionKind.Payment, row.Transaction.Kind);
        Assert.Equal(0, preview.SkippedCount);
    }

    [Fact]
    public void Parse_TreatsBlankFeesAsZero()
    {
        const string csv = """
            Date,Time,TimeZone,Name,Type,Status,Currency,Gross,Fee,Net,Transaction ID,Reference Txn ID,Balance Impact
            06/01/2026,10:15:30,CDT,,Bank Transfer to Bank,Completed,USD,-50.00,,-50.00,TRN-001,,Debit
            """;

        var preview = CreateParser().Parse(CreateRequest(csv));

        var transaction = Assert.Single(preview.Rows).Transaction!;
        Assert.Equal(0m, transaction.FeeAmount);
        Assert.Equal(-50m, transaction.NetAmount);
    }

    [Fact]
    public void Parse_AcceptsCsvWithoutBalanceImpact()
    {
        const string csv = """
            Date,Time,TimeZone,Name,Type,Status,Currency,Gross,Fee,Net,Transaction ID,Reference Txn ID
            06/01/2026,10:15:30,CDT,Fake Customer,Express Checkout Payment,Completed,USD,100.00,-3.49,96.51,PAY-001,
            """;

        var preview = CreateParser().Parse(CreateRequest(csv));

        Assert.Equal(1, preview.ValidCount);
        Assert.Equal(0, preview.ErrorCount);
        Assert.Equal(0, preview.SkippedCount);
    }

    [Fact]
    public void Parse_AllowsBlankTransactionIdsAndUsesFallbackFingerprint()
    {
        const string csv = """
            Date,Time,TimeZone,Name,Type,Status,Currency,Gross,Fee,Net,Transaction ID,Reference Txn ID,Balance Impact
            06/01/2026,10:15:30,CDT,PayPal Adjustment,General Adjustment,Completed,USD,12.00,0.00,12.00,,,Credit
            """;

        var preview = CreateParser().Parse(CreateRequest(csv));

        var transaction = Assert.Single(preview.Rows).Transaction!;
        Assert.Null(transaction.SourceTransactionId);
        Assert.Equal(
            ImportFingerprint.Create(
                CsvImportProfiles.PayPalName,
                new DateOnly(2026, 6, 1),
                12m,
                "PayPal Adjustment"),
            transaction.Fingerprint);
    }

    private static PayPalCsvParser CreateParser() => new();

    private static CsvImportRequest CreateRequest(string csv)
    {
        var headers = new GenericCsvParser().ReadHeaders(csv);
        var mapping = CsvImportProfiles
            .Get(CsvImportProfiles.PayPalName)
            .CreateMapping(headers);
        return new CsvImportRequest(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            CsvImportProfiles.PayPalName,
            "paypal.csv",
            csv,
            mapping);
    }

    private static string ReadFixture()
    {
        return File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "paypal.csv"));
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
