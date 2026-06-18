using FinancialApp.Importers;

namespace FinancialApp.Api.Imports;

public sealed record CsvImportRowResponse(
    int RowNumber,
    DateOnly? PostedDate,
    string? Description,
    decimal? Amount,
    string? Currency,
    string? Error)
{
    public static CsvImportRowResponse FromRow(CsvImportRow row)
    {
        return new CsvImportRowResponse(
            row.RowNumber,
            row.Transaction?.PostedDate,
            row.Transaction?.Description,
            row.Transaction?.Amount,
            row.Transaction?.Currency,
            row.Error);
    }
}
