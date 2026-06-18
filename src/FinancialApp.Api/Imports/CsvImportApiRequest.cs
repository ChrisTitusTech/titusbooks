using FinancialApp.Importers;

namespace FinancialApp.Api.Imports;

public sealed record CsvImportApiRequest(
    string Source,
    string? FileName,
    string CsvContent,
    CsvColumnMappingRequest Mapping)
{
    public CsvImportRequest ToImportRequest(Guid organizationId)
    {
        return new CsvImportRequest(
            organizationId,
            Source,
            FileName,
            CsvContent,
            Mapping.ToMapping());
    }
}
