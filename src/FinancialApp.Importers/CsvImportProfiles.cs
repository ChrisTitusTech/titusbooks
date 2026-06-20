namespace FinancialApp.Importers;

public static class CsvImportProfiles
{
    public const string GenericCsvName = "Generic CSV";
    public const string BankOfAmericaName = "Bank of America";

    public static IReadOnlyList<CsvImportProfile> All { get; } =
    [
        new(GenericCsvName, GenericCsvName, CreateGenericMapping),
        new(BankOfAmericaName, BankOfAmericaName, CreateBankOfAmericaMapping)
    ];

    public static CsvImportProfile Get(string name)
    {
        return All.FirstOrDefault(profile =>
                string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unknown CSV import profile '{name}'.");
    }

    private static CsvColumnMapping CreateGenericMapping(IReadOnlyList<string> headers)
    {
        var amountColumn = FindHeader(
            headers,
            "amount",
            "transaction amount",
            "signed amount",
            "net",
            "value");
        return new CsvColumnMapping(
            FindHeader(headers, "date", "posted") ?? string.Empty,
            FindHeader(headers, "description", "memo", "name", "details") ?? string.Empty,
            AmountColumn: amountColumn,
            DebitColumn: amountColumn is null ? FindHeader(headers, "debit", "withdrawal") : null,
            CreditColumn: amountColumn is null ? FindHeader(headers, "credit", "deposit") : null,
            SourceTransactionIdColumn: FindHeader(headers, "transaction id", "id", "reference"),
            CurrencyColumn: FindHeader(headers, "currency"),
            BalanceColumn: FindHeader(headers, "balance", "running balance"));
    }

    private static CsvColumnMapping CreateBankOfAmericaMapping(IReadOnlyList<string> headers)
    {
        return new CsvColumnMapping(
            FindRequiredHeader(headers, "Posted Date", "Posting Date", "Date"),
            FindRequiredHeader(headers, "Payee", "Description", "Memo"),
            AmountColumn: FindRequiredHeader(headers, "Amount"),
            SourceTransactionIdColumn: FindHeader(
                headers,
                "Reference Number",
                "Reference #",
                "Transaction ID"),
            CurrencyColumn: FindHeader(headers, "Currency"),
            BalanceColumn: FindHeader(
                headers,
                "Running Bal.",
                "Running Balance",
                "Balance"),
            SkipBalanceOnlyRows: true);
    }

    private static string FindRequiredHeader(
        IReadOnlyList<string> headers,
        params string[] candidates)
    {
        return FindHeader(headers, candidates)
            ?? throw new InvalidOperationException(
                $"CSV profile requires one of these columns: {string.Join(", ", candidates)}.");
    }

    private static string? FindHeader(
        IEnumerable<string> headers,
        params string[] candidates)
    {
        var headerList = headers.ToList();
        var exactMatch = headerList.FirstOrDefault(header =>
            candidates.Any(candidate =>
                string.Equals(header, candidate, StringComparison.OrdinalIgnoreCase)));
        if (exactMatch is not null)
        {
            return exactMatch;
        }

        return headerList.FirstOrDefault(header =>
            candidates
                .Where(candidate => candidate.Length >= 4)
                .Any(candidate => header.Contains(candidate, StringComparison.OrdinalIgnoreCase)));
    }
}
