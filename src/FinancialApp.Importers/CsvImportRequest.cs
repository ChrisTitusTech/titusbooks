namespace FinancialApp.Importers;

public sealed record CsvImportRequest(
    Guid OrganizationId,
    string Source,
    string? FileName,
    string CsvContent,
    CsvColumnMapping Mapping);
