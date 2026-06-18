using FinancialApp.Core.Imports;

namespace FinancialApp.Importers;

public sealed record CsvImportRow(
    int RowNumber,
    ImportedTransaction? Transaction,
    string? Error,
    IReadOnlyDictionary<string, string?> RawValues)
{
    public bool IsValid => Transaction is not null && Error is null;
}
