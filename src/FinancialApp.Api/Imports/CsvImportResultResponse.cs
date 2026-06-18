using FinancialApp.Importers;

namespace FinancialApp.Api.Imports;

public sealed record CsvImportResultResponse(
    Guid ImportBatchId,
    int PendingCount,
    int DuplicateCount,
    int ErrorCount)
{
    public static CsvImportResultResponse FromResult(CsvImportResult result)
    {
        return new CsvImportResultResponse(
            result.ImportBatchId,
            result.PendingCount,
            result.DuplicateCount,
            result.ErrorCount);
    }
}
