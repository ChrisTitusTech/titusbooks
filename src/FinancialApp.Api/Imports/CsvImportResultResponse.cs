using FinancialApp.Importers;

namespace FinancialApp.Api.Imports;

public sealed record CsvImportResultResponse(
    Guid? ImportBatchId,
    int PendingCount,
    int CategorizedCount,
    int DuplicateCount,
    int ErrorCount,
    int SkippedCount)
{
    public static CsvImportResultResponse FromResult(CsvImportResult result)
    {
        return new CsvImportResultResponse(
            result.ImportBatchId,
            result.PendingCount,
            result.CategorizedCount,
            result.DuplicateCount,
            result.ErrorCount,
            result.SkippedCount);
    }
}
