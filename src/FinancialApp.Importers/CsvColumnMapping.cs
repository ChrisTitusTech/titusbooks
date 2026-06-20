namespace FinancialApp.Importers;

public sealed record CsvColumnMapping(
    string DateColumn,
    string DescriptionColumn,
    string? AmountColumn = null,
    string? DebitColumn = null,
    string? CreditColumn = null,
    string? SourceTransactionIdColumn = null,
    string? CurrencyColumn = null,
    string DefaultCurrency = "USD",
    string? BalanceColumn = null,
    bool SkipBalanceOnlyRows = false);
