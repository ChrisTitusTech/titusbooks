using System.Globalization;
using System.Text;
using System.Text.Json;
using FinancialApp.Core.Imports;
using Microsoft.VisualBasic.FileIO;

namespace FinancialApp.Importers;

public sealed class PayPalCsvParser
{
    private const int MaximumCsvByteCount = 10 * 1024 * 1024;

    private static readonly string[] RequiredHeaders =
    [
        "Date",
        "Time",
        "TimeZone",
        "Name",
        "Type",
        "Status",
        "Currency",
        "Gross",
        "Fee",
        "Net",
        "Transaction ID",
        "Reference Txn ID"
    ];

    public CsvImportPreview Parse(CsvImportRequest request)
    {
        ValidateRequest(request);

        using var reader = new StringReader(request.CsvContent);
        using var parser = CreateParser(reader);
        var headers = parser.ReadFields()?.Select(header => header.Trim()).ToArray()
            ?? throw new InvalidOperationException("PayPal CSV does not contain a header row.");
        ValidateHeaders(headers);

        var rows = new List<CsvImportRow>();
        var skippedCount = 0;
        var rowNumber = 1;
        while (!parser.EndOfData)
        {
            rowNumber++;
            try
            {
                var fields = parser.ReadFields() ?? [];
                if (fields.Length > headers.Length)
                {
                    rows.Add(new CsvImportRow(
                        rowNumber,
                        null,
                        $"PayPal CSV row has {fields.Length} fields but the header defines {headers.Length}.",
                        BuildRawValues(headers, fields)));
                    continue;
                }

                var rawValues = BuildRawValues(headers, fields);
                if (ShouldSkip(rawValues))
                {
                    skippedCount++;
                    continue;
                }

                rows.Add(ParseRow(request, rowNumber, rawValues));
            }
            catch (MalformedLineException exception)
            {
                rows.Add(new CsvImportRow(
                    rowNumber,
                    null,
                    $"Malformed PayPal CSV row: {exception.Message}",
                    new Dictionary<string, string?>()));
            }
        }

        return new CsvImportPreview(headers, rows, skippedCount);
    }

    private static TextFieldParser CreateParser(TextReader reader)
    {
        var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");
        return parser;
    }

    private static CsvImportRow ParseRow(
        CsvImportRequest request,
        int rowNumber,
        IReadOnlyDictionary<string, string?> rawValues)
    {
        try
        {
            var date = ParseDate(GetRequired(rawValues, "Date"));
            var time = ParseTime(GetRequired(rawValues, "Time"));
            var type = GetRequired(rawValues, "Type").Trim();
            var status = GetRequired(rawValues, "Status").Trim();
            var gross = ParseDecimal(GetRequired(rawValues, "Gross"));
            var rawFee = GetOptional(rawValues, "Fee") is { } feeValue
                ? ParseDecimal(feeValue)
                : 0m;
            var net = ParseDecimal(GetRequired(rawValues, "Net"));
            if (gross + rawFee != net)
            {
                throw new InvalidOperationException("PayPal gross plus fee must equal net.");
            }

            var fee = Math.Abs(rawFee);
            var transactionId = GetOptional(rawValues, "Transaction ID");
            var name = GetOptional(rawValues, "Name");
            var description = name
                ?? GetOptional(rawValues, "Item Title")
                ?? type;
            var currency = GetRequired(rawValues, "Currency").Trim().ToUpperInvariant();
            if (currency.Length != 3)
            {
                throw new InvalidOperationException("PayPal currency must use a three-letter code.");
            }

            var transaction = new ImportedTransaction
            {
                Id = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                Source = request.Source.Trim(),
                SourceTransactionId = transactionId,
                ReferenceTransactionId = GetOptional(rawValues, "Reference Txn ID"),
                SourceType = type,
                SourceStatus = status,
                PostedDate = date,
                PostedTime = time,
                SourceTimeZone = GetOptional(rawValues, "TimeZone"),
                Description = description,
                RawDescription = name,
                Amount = net,
                GrossAmount = gross,
                FeeAmount = fee,
                NetAmount = net,
                Currency = currency,
                Kind = Classify(type),
                Status = ImportedTransactionStatus.Pending,
                Fingerprint = ImportFingerprint.Create(
                    request.Source,
                    date,
                    net,
                    description,
                    transactionId),
                RawPayloadJson = JsonSerializer.Serialize(rawValues)
            };
            return new CsvImportRow(rowNumber, transaction, null, rawValues);
        }
        catch (InvalidOperationException exception)
        {
            return new CsvImportRow(rowNumber, null, exception.Message, rawValues);
        }
    }

    private static bool ShouldSkip(IReadOnlyDictionary<string, string?> rawValues)
    {
        var status = GetOptional(rawValues, "Status");
        var balanceImpact = GetOptional(rawValues, "Balance Impact");
        var type = GetOptional(rawValues, "Type");

        return string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(balanceImpact, "Memo", StringComparison.OrdinalIgnoreCase)
            || type?.Contains("authorization", StringComparison.OrdinalIgnoreCase) == true
            || type?.Contains("account hold", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static ImportedTransactionKind Classify(string type)
    {
        if (type.Contains("refund", StringComparison.OrdinalIgnoreCase))
        {
            return ImportedTransactionKind.Refund;
        }

        if (type.Contains("currency conversion", StringComparison.OrdinalIgnoreCase))
        {
            return ImportedTransactionKind.CurrencyConversion;
        }

        if (type.Contains("transfer", StringComparison.OrdinalIgnoreCase)
            || type.Contains("deposit to pp account", StringComparison.OrdinalIgnoreCase)
            || type.Contains("card deposit", StringComparison.OrdinalIgnoreCase))
        {
            return ImportedTransactionKind.Transfer;
        }

        if (type.Contains("fee", StringComparison.OrdinalIgnoreCase))
        {
            return ImportedTransactionKind.Fee;
        }

        if (type.Contains("payment", StringComparison.OrdinalIgnoreCase)
            || type.Contains("checkout", StringComparison.OrdinalIgnoreCase)
            || type.Contains("debit card transaction", StringComparison.OrdinalIgnoreCase))
        {
            return ImportedTransactionKind.Payment;
        }

        return ImportedTransactionKind.Other;
    }

    private static DateOnly ParseDate(string value)
    {
        if (DateOnly.TryParseExact(
            value.Trim(),
            ["M/d/yyyy", "MM/dd/yyyy", "M/d/yy", "MM/dd/yy"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date))
        {
            return date;
        }

        throw new InvalidOperationException($"Invalid PayPal date '{value}'.");
    }

    private static TimeOnly ParseTime(string value)
    {
        if (TimeOnly.TryParseExact(
            value.Trim(),
            ["H:mm:ss", "HH:mm:ss", "h:mm:ss tt", "hh:mm:ss tt"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var time))
        {
            return time;
        }

        throw new InvalidOperationException($"Invalid PayPal time '{value}'.");
    }

    private static decimal ParseDecimal(string value)
    {
        var normalized = value.Trim()
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        if (decimal.TryParse(
            normalized,
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var amount))
        {
            return amount;
        }

        throw new InvalidOperationException($"Invalid PayPal amount '{value}'.");
    }

    private static IReadOnlyDictionary<string, string?> BuildRawValues(
        IReadOnlyList<string> headers,
        IReadOnlyList<string> fields)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            values[headers[index]] = index < fields.Count ? fields[index] : null;
        }

        return values;
    }

    private static string GetRequired(
        IReadOnlyDictionary<string, string?> values,
        string column)
    {
        return GetOptional(values, column)
            ?? throw new InvalidOperationException($"PayPal column '{column}' is required.");
    }

    private static string? GetOptional(
        IReadOnlyDictionary<string, string?> values,
        string column)
    {
        return values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static void ValidateHeaders(IReadOnlyList<string> headers)
    {
        if (headers.Count == 0 || headers.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("PayPal CSV header names cannot be empty.");
        }

        var duplicateHeader = headers
            .GroupBy(header => header, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;
        if (duplicateHeader is not null)
        {
            throw new InvalidOperationException(
                $"PayPal CSV contains duplicate header '{duplicateHeader}'.");
        }

        foreach (var requiredHeader in RequiredHeaders)
        {
            if (!headers.Contains(requiredHeader, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"PayPal CSV does not contain required column '{requiredHeader}'.");
            }
        }
    }

    private static void ValidateRequest(CsvImportRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            throw new InvalidOperationException("Organization is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Source))
        {
            throw new InvalidOperationException("Import source is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CsvContent))
        {
            throw new InvalidOperationException("CSV content is required.");
        }

        if (Encoding.UTF8.GetByteCount(request.CsvContent) > MaximumCsvByteCount)
        {
            throw new InvalidOperationException("CSV content exceeds the 10 MB import limit.");
        }
    }
}
