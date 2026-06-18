using FinancialApp.Importers;

namespace FinancialApp.Api.Imports;

public sealed record CsvColumnMappingRequest(
    string DateColumn,
    string DescriptionColumn,
    string? AmountColumn = null,
    string? DebitColumn = null,
    string? CreditColumn = null,
    string? SourceTransactionIdColumn = null,
    string? CurrencyColumn = null,
    string DefaultCurrency = "USD")
{
    public CsvColumnMapping ToMapping()
    {
        return new CsvColumnMapping(
            DateColumn,
            DescriptionColumn,
            AmountColumn,
            DebitColumn,
            CreditColumn,
            SourceTransactionIdColumn,
            CurrencyColumn,
            DefaultCurrency);
    }
}
