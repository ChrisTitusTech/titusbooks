using FinancialApp.Importers;

namespace FinancialApp.Api.Imports;

public sealed record CsvImportPreviewResponse(
    IReadOnlyList<string> Headers,
    IReadOnlyList<CsvImportRowResponse> Rows,
    int ValidCount,
    int ErrorCount)
{
    public static CsvImportPreviewResponse FromPreview(CsvImportPreview preview)
    {
        return new CsvImportPreviewResponse(
            preview.Headers,
            preview.Rows.Select(CsvImportRowResponse.FromRow).ToList(),
            preview.ValidCount,
            preview.ErrorCount);
    }
}
