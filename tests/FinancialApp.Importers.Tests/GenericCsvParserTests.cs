using System.Globalization;
using System.Text.Json;
using FinancialApp.Importers;

namespace FinancialApp.Importers.Tests;

public sealed class GenericCsvParserTests
{
    [Fact]
    public void Parse_SignedAmountColumns_NormalizesRowsAndPreservesRawPayload()
    {
        const string csv = """
            Date,Description,Amount,Currency,Transaction ID
            2026-06-01,"Office, supplies",-42.50,usd,abc-1
            06/02/2026,Client payment,"1,250.00",USD,abc-2
            """;
        var request = CreateRequest(
            csv,
            new CsvColumnMapping(
                "Date",
                "Description",
                AmountColumn: "Amount",
                SourceTransactionIdColumn: "Transaction ID",
                CurrencyColumn: "Currency"));

        var preview = new GenericCsvParser().Parse(request);

        Assert.Equal(2, preview.ValidCount);
        Assert.Equal(0, preview.ErrorCount);
        Assert.Equal(-42.50m, preview.Rows[0].Transaction!.Amount);
        Assert.Equal("Office, supplies", preview.Rows[0].Transaction!.Description);
        Assert.Equal(1250m, preview.Rows[1].Transaction!.Amount);
        using var rawPayload = JsonDocument.Parse(preview.Rows[0].Transaction!.RawPayloadJson!);
        Assert.Equal("abc-1", rawPayload.RootElement.GetProperty("Transaction ID").GetString());
    }

    [Fact]
    public void Parse_DebitCreditColumns_UsesSignedNormalizedAmount()
    {
        const string csv = """
            Posted,Memo,Debit,Credit
            2026-06-01,Coffee,5.25,
            2026-06-02,Refund,,2.00
            """;
        var request = CreateRequest(
            csv,
            new CsvColumnMapping(
                "Posted",
                "Memo",
                DebitColumn: "Debit",
                CreditColumn: "Credit"));

        var preview = new GenericCsvParser().Parse(request);

        Assert.Equal(-5.25m, preview.Rows[0].Transaction!.Amount);
        Assert.Equal(2m, preview.Rows[1].Transaction!.Amount);
    }

    [Fact]
    public void Parse_InvalidRow_ReturnsRowErrorWithoutFailingValidRows()
    {
        const string csv = """
            Date,Description,Amount
            2026-06-01,Valid,10.00
            not-a-date,Bad date,12.00
            """;
        var request = CreateRequest(
            csv,
            new CsvColumnMapping("Date", "Description", AmountColumn: "Amount"));

        var preview = new GenericCsvParser().Parse(request);

        Assert.Equal(1, preview.ValidCount);
        Assert.Equal(1, preview.ErrorCount);
        Assert.Contains("Invalid date", preview.Rows[1].Error);
    }

    [Fact]
    public void ReadHeaders_ReturnsQuotedCsvHeaders()
    {
        const string csv = "\"Posted Date\",\"Description\",\"Amount\"\n2026-06-01,Test,1.00";

        var headers = new GenericCsvParser().ReadHeaders(csv);

        Assert.Equal(["Posted Date", "Description", "Amount"], headers);
    }

    [Fact]
    public void Parse_UsesSourceTransactionIdForFingerprintWhenAvailable()
    {
        const string csv = """
            Date,Description,Amount,Transaction ID
            2026-06-01,Same description,10.00,id-1
            2026-06-01,Same description,10.00,id-2
            """;
        var request = CreateRequest(
            csv,
            new CsvColumnMapping(
                "Date",
                "Description",
                AmountColumn: "Amount",
                SourceTransactionIdColumn: "Transaction ID"));

        var preview = new GenericCsvParser().Parse(request);

        Assert.NotEqual(
            preview.Rows[0].Transaction!.Fingerprint,
            preview.Rows[1].Transaction!.Fingerprint);
    }

    [Fact]
    public void Parse_RejectsEmptyHeaderNames()
    {
        var request = CreateRequest(
            "Date,,Amount\n2026-06-01,Description,10.00",
            new CsvColumnMapping("Date", "Description", AmountColumn: "Amount"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => new GenericCsvParser().Parse(request));

        Assert.Equal("CSV header names cannot be empty.", exception.Message);
    }

    [Fact]
    public void Fingerprint_IsStableAcrossCultures()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var englishFingerprint = ImportFingerprint.Create(
                "Generic CSV",
                new DateOnly(2026, 6, 1),
                1234.56m,
                "Office supplies");

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var frenchFingerprint = ImportFingerprint.Create(
                "Generic CSV",
                new DateOnly(2026, 6, 1),
                1234.56m,
                "Office supplies");

            Assert.Equal(englishFingerprint, frenchFingerprint);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static CsvImportRequest CreateRequest(string csv, CsvColumnMapping mapping)
    {
        return new CsvImportRequest(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "Generic CSV",
            "fake.csv",
            csv,
            mapping);
    }
}
