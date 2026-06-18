namespace FinancialApp.Core.Api;

public sealed record CsvImportCommand(
    string Source,
    string? FileName,
    string CsvContent,
    CsvColumnMappingCommand Mapping);
