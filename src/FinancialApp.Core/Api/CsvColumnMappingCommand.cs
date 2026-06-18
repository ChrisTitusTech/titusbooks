namespace FinancialApp.Core.Api;

public sealed record CsvColumnMappingCommand(
    string DateColumn,
    string DescriptionColumn,
    string? AmountColumn = null,
    string? DebitColumn = null,
    string? CreditColumn = null,
    string? SourceTransactionIdColumn = null,
    string? CurrencyColumn = null,
    string DefaultCurrency = "USD");
