using System.Globalization;
using System.Text.Json;
using FinancialApp.Core.Imports;
using Microsoft.VisualBasic.FileIO;

namespace FinancialApp.Importers;

public sealed class GenericCsvParser
{
    private static readonly string[] SupportedDateFormats =
    [
        "yyyy-MM-dd",
        "M/d/yyyy",
        "MM/dd/yyyy",
        "M/d/yy",
        "MM/dd/yy",
        "yyyyMMdd"
    ];

    public CsvImportPreview Parse(CsvImportRequest request)
    {
        ValidateRequest(request);

        using var reader = new StringReader(request.CsvContent);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        var headers = ReadHeaderFields(parser);
        ValidateHeaders(headers, request.Mapping);

        var rows = new List<CsvImportRow>();
        var rowNumber = 1;
        while (!parser.EndOfData)
        {
            rowNumber++;
            try
            {
                var fields = parser.ReadFields() ?? [];
                var rawValues = BuildRawValues(headers, fields);
                rows.Add(ParseRow(request, rowNumber, rawValues));
            }
            catch (MalformedLineException exception)
            {
                rows.Add(new CsvImportRow(
                    rowNumber,
                    null,
                    $"Malformed CSV row: {exception.Message}",
                    new Dictionary<string, string?>()));
            }
        }

        return new CsvImportPreview(headers, rows);
    }

    public IReadOnlyList<string> ReadHeaders(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            throw new InvalidOperationException("CSV content is required.");
        }

        using var reader = new StringReader(csvContent);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        var headers = ReadHeaderFields(parser);
        ValidateHeaderNames(headers);
        return headers;
    }

    private static string[] ReadHeaderFields(TextFieldParser parser)
    {
        try
        {
            return parser.ReadFields()?.Select(header => header.Trim()).ToArray()
                ?? throw new InvalidOperationException("CSV file does not contain a header row.");
        }
        catch (MalformedLineException exception)
        {
            throw new InvalidOperationException(
                $"Malformed CSV header: {exception.Message}",
                exception);
        }
    }

    private static CsvImportRow ParseRow(
        CsvImportRequest request,
        int rowNumber,
        IReadOnlyDictionary<string, string?> rawValues)
    {
        try
        {
            var dateText = GetRequiredValue(rawValues, request.Mapping.DateColumn);
            var description = GetRequiredValue(rawValues, request.Mapping.DescriptionColumn).Trim();
            var postedDate = ParseDate(dateText);
            var amount = ParseAmount(rawValues, request.Mapping);
            var currency = GetOptionalValue(rawValues, request.Mapping.CurrencyColumn)
                ?? request.Mapping.DefaultCurrency;
            var sourceTransactionId = GetOptionalValue(
                rawValues,
                request.Mapping.SourceTransactionIdColumn);

            if (description.Length == 0)
            {
                throw new InvalidOperationException("Description is required.");
            }

            if (currency.Trim().Length != 3)
            {
                throw new InvalidOperationException("Currency must use a three-letter code.");
            }

            var transaction = new ImportedTransaction
            {
                Id = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                Source = request.Source.Trim(),
                SourceTransactionId = sourceTransactionId,
                PostedDate = postedDate,
                Description = description,
                RawDescription = description,
                Amount = amount,
                Currency = currency.Trim().ToUpperInvariant(),
                Status = ImportedTransactionStatus.Pending,
                Fingerprint = ImportFingerprint.Create(request.Source, postedDate, amount, description),
                RawPayloadJson = JsonSerializer.Serialize(rawValues)
            };

            return new CsvImportRow(rowNumber, transaction, null, rawValues);
        }
        catch (InvalidOperationException exception)
        {
            return new CsvImportRow(rowNumber, null, exception.Message, rawValues);
        }
    }

    private static decimal ParseAmount(
        IReadOnlyDictionary<string, string?> rawValues,
        CsvColumnMapping mapping)
    {
        if (!string.IsNullOrWhiteSpace(mapping.AmountColumn))
        {
            return ParseDecimal(GetRequiredValue(rawValues, mapping.AmountColumn));
        }

        var debit = ParseOptionalDecimal(GetOptionalValue(rawValues, mapping.DebitColumn));
        var credit = ParseOptionalDecimal(GetOptionalValue(rawValues, mapping.CreditColumn));
        if (debit == 0 && credit == 0)
        {
            throw new InvalidOperationException("Debit or credit amount is required.");
        }

        return credit - debit;
    }

    private static decimal ParseDecimal(string value)
    {
        var normalized = value.Trim()
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        var isParenthesized = normalized.StartsWith('(') && normalized.EndsWith(')');
        if (isParenthesized)
        {
            normalized = $"-{normalized[1..^1]}";
        }

        if (!decimal.TryParse(
            normalized,
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var amount))
        {
            throw new InvalidOperationException($"Invalid amount '{value}'.");
        }

        return amount;
    }

    private static decimal ParseOptionalDecimal(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? 0 : ParseDecimal(value);
    }

    private static DateOnly ParseDate(string value)
    {
        if (DateOnly.TryParseExact(
            value.Trim(),
            SupportedDateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var date))
        {
            return date;
        }

        throw new InvalidOperationException($"Invalid date '{value}'.");
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

    private static string GetRequiredValue(
        IReadOnlyDictionary<string, string?> values,
        string column)
    {
        if (!values.TryGetValue(column, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Column '{column}' is required.");
        }

        return value;
    }

    private static string? GetOptionalValue(
        IReadOnlyDictionary<string, string?> values,
        string? column)
    {
        if (string.IsNullOrWhiteSpace(column)
            || !values.TryGetValue(column, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
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

        if (string.IsNullOrWhiteSpace(request.Mapping.DateColumn)
            || string.IsNullOrWhiteSpace(request.Mapping.DescriptionColumn))
        {
            throw new InvalidOperationException("Date and description columns are required.");
        }

        var usesAmount = !string.IsNullOrWhiteSpace(request.Mapping.AmountColumn);
        var usesDebitCredit = !string.IsNullOrWhiteSpace(request.Mapping.DebitColumn)
            || !string.IsNullOrWhiteSpace(request.Mapping.CreditColumn);
        if (usesAmount == usesDebitCredit)
        {
            throw new InvalidOperationException(
                "Map either one amount column or debit/credit columns.");
        }
    }

    private static void ValidateHeaders(
        IReadOnlyList<string> headers,
        CsvColumnMapping mapping)
    {
        ValidateHeaderNames(headers);

        var mappedColumns = new[]
        {
            mapping.DateColumn,
            mapping.DescriptionColumn,
            mapping.AmountColumn,
            mapping.DebitColumn,
            mapping.CreditColumn,
            mapping.SourceTransactionIdColumn,
            mapping.CurrencyColumn
        }.Where(column => !string.IsNullOrWhiteSpace(column));

        foreach (var column in mappedColumns)
        {
            if (!headers.Contains(column!, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"CSV does not contain column '{column}'.");
            }
        }
    }

    private static void ValidateHeaderNames(IReadOnlyList<string> headers)
    {
        if (headers.Count == 0 || headers.All(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("CSV header row is empty.");
        }

        var duplicates = headers
            .GroupBy(header => header, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"CSV contains duplicate header '{duplicates[0]}'.");
        }
    }
}
