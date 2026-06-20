using System.Globalization;
using System.Text;
using System.Text.Json;
using FinancialApp.Core.Imports;
using Microsoft.VisualBasic.FileIO;

namespace FinancialApp.Importers;

public sealed class GenericCsvParser
{
    private const int MaximumCsvByteCount = 10 * 1024 * 1024;

    private static readonly string[] SupportedDateFormats =
    [
        "yyyy-MM-dd",
        "M/d/yyyy",
        "MM/dd/yyyy",
        "M/d/yy",
        "MM/dd/yy",
        "yyyyMMdd",
        "M/d/yyyy h:mm tt",
        "MM/dd/yyyy hh:mm tt",
        "M/d/yyyy H:mm:ss",
        "MM/dd/yyyy HH:mm:ss"
    ];

    public CsvImportPreview Parse(CsvImportRequest request)
    {
        ValidateRequest(request);

        using var reader = new StringReader(request.CsvContent);
        using var parser = CreateParser(reader);

        var header = ReadHeaderFields(parser, request.Mapping);
        var headers = header.Fields;
        ValidateHeaders(headers, request.Mapping);

        var rows = new List<CsvImportRow>();
        var rowNumber = header.RowNumber;
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
                        $"CSV row has {fields.Length} fields but the header defines {headers.Length}.",
                        BuildRawValues(headers, fields)));
                    continue;
                }

                var rawValues = BuildRawValues(headers, fields);
                if (ShouldSkipBalanceOnlyRow(rawValues, request.Mapping))
                {
                    continue;
                }

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
        ValidateCsvContent(csvContent);

        using var reader = new StringReader(csvContent);
        using var parser = CreateParser(reader);

        var headers = ReadHeaderFields(parser).Fields;
        ValidateHeaderNames(headers);
        return headers;
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

    private static CsvHeader ReadHeaderFields(
        TextFieldParser parser,
        CsvColumnMapping? mapping = null)
    {
        var rowNumber = 0;
        while (!parser.EndOfData)
        {
            rowNumber++;
            try
            {
                var fields = parser.ReadFields()?.Select(header => header.Trim()).ToArray() ?? [];
                if (mapping is null
                    && rowNumber == 1
                    && fields.Length > 0
                    && fields.All(field => !string.IsNullOrWhiteSpace(field)))
                {
                    return new CsvHeader(fields, rowNumber);
                }

                if (mapping is not null
                    && rowNumber == 1
                    && ContainsMappedHeader(fields, mapping.DateColumn)
                    && ContainsMappedHeader(
                        fields,
                        mapping.AmountColumn,
                        mapping.DebitColumn,
                        mapping.CreditColumn))
                {
                    return new CsvHeader(fields, rowNumber);
                }

                if (IsHeaderRow(fields, mapping))
                {
                    return new CsvHeader(fields, rowNumber);
                }
            }
            catch (MalformedLineException exception)
            {
                throw new InvalidOperationException(
                    $"Malformed CSV header: {exception.Message}",
                    exception);
            }
        }

        throw new InvalidOperationException("CSV file does not contain a recognizable header row.");
    }

    private static bool IsHeaderRow(
        IReadOnlyList<string> fields,
        CsvColumnMapping? mapping)
    {
        if (fields.Count == 0 || fields.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        if (mapping is not null)
        {
            var requiredColumns = new[]
            {
                mapping.DateColumn,
                mapping.DescriptionColumn,
                mapping.AmountColumn,
                mapping.DebitColumn,
                mapping.CreditColumn
            }.Where(column => !string.IsNullOrWhiteSpace(column));
            return requiredColumns.All(column =>
                fields.Contains(column!, StringComparer.OrdinalIgnoreCase));
        }

        var hasDate = ContainsAny(fields, "Date", "Posted Date", "Posting Date");
        var hasDescription = ContainsAny(fields, "Description", "Payee", "Memo", "Name", "Details");
        var hasAmount = ContainsAny(
            fields,
            "Amount",
            "Transaction Amount",
            "Signed Amount",
            "Debit",
            "Credit",
            "Withdrawal",
            "Deposit",
            "Net",
            "Value");
        return hasDate && hasDescription && hasAmount;
    }

    private static bool ContainsAny(
        IReadOnlyList<string> fields,
        params string[] candidates)
    {
        return fields.Any(field =>
            candidates.Any(candidate =>
                string.Equals(field, candidate, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ContainsMappedHeader(
        IReadOnlyList<string> fields,
        params string?[] candidates)
    {
        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Any(candidate =>
                fields.Contains(candidate!, StringComparer.OrdinalIgnoreCase));
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
            var balance = ParseOptionalBalance(rawValues, request.Mapping);
            var currency = GetOptionalValue(rawValues, request.Mapping.CurrencyColumn)
                ?? request.Mapping.DefaultCurrency;
            var normalizedCurrency = NormalizeCurrency(currency);
            var sourceTransactionId = GetOptionalValue(
                rawValues,
                request.Mapping.SourceTransactionIdColumn);

            if (description.Length == 0)
            {
                throw new InvalidOperationException("Description is required.");
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
                Balance = balance,
                Currency = normalizedCurrency,
                Status = ImportedTransactionStatus.Pending,
                Fingerprint = ImportFingerprint.Create(
                    request.Source,
                    postedDate,
                    amount,
                    description,
                    sourceTransactionId),
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
        if (debit != 0 && credit != 0)
        {
            throw new InvalidOperationException(
                "A row cannot contain both debit and credit amounts.");
        }

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
        var isCredit = normalized.EndsWith(" CR", StringComparison.OrdinalIgnoreCase);
        var isDebit = normalized.EndsWith(" DR", StringComparison.OrdinalIgnoreCase);
        if (isCredit || isDebit)
        {
            normalized = normalized[..^3].TrimEnd();
        }

        if (normalized.EndsWith('-'))
        {
            normalized = $"-{normalized[..^1]}";
        }

        var isParenthesized = normalized.StartsWith('(') && normalized.EndsWith(')');
        if (isParenthesized)
        {
            normalized = $"-{normalized[1..^1]}";
        }

        if (isDebit && !normalized.StartsWith('-'))
        {
            normalized = $"-{normalized}";
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

    private static decimal? ParseOptionalBalance(
        IReadOnlyDictionary<string, string?> rawValues,
        CsvColumnMapping mapping)
    {
        var value = GetOptionalValue(rawValues, mapping.BalanceColumn);
        return value is null ? null : ParseDecimal(value);
    }

    private static bool ShouldSkipBalanceOnlyRow(
        IReadOnlyDictionary<string, string?> rawValues,
        CsvColumnMapping mapping)
    {
        if (!mapping.SkipBalanceOnlyRows
            || string.IsNullOrWhiteSpace(mapping.AmountColumn)
            || string.IsNullOrWhiteSpace(mapping.BalanceColumn))
        {
            return false;
        }

        var amount = GetOptionalValue(rawValues, mapping.AmountColumn);
        var balance = GetOptionalValue(rawValues, mapping.BalanceColumn);
        var description = GetOptionalValue(rawValues, mapping.DescriptionColumn);
        return amount is null
            && balance is not null
            && description?.StartsWith("Beginning balance", StringComparison.OrdinalIgnoreCase) == true;
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

        ValidateCsvContent(request.CsvContent);
        _ = NormalizeCurrency(request.Mapping.DefaultCurrency);

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

    private static void ValidateCsvContent(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            throw new InvalidOperationException("CSV content is required.");
        }

        if (Encoding.UTF8.GetByteCount(csvContent) > MaximumCsvByteCount)
        {
            throw new InvalidOperationException("CSV content exceeds the 10 MB import limit.");
        }
    }

    private static string NormalizeCurrency(string? currency)
    {
        var normalizedCurrency = currency?.Trim().ToUpperInvariant();
        if (normalizedCurrency?.Length != 3
            || normalizedCurrency.Any(character => !char.IsAsciiLetter(character)))
        {
            throw new InvalidOperationException("Currency must use a three-letter code.");
        }

        return normalizedCurrency;
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
            mapping.CurrencyColumn,
            mapping.BalanceColumn
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
        if (headers.Count == 0 || headers.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("CSV header names cannot be empty.");
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

    private sealed record CsvHeader(string[] Fields, int RowNumber);
}
