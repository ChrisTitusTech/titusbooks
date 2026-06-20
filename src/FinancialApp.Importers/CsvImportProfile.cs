namespace FinancialApp.Importers;

public sealed record CsvImportProfile(
    string Name,
    string Source,
    Func<IReadOnlyList<string>, CsvColumnMapping> CreateMapping);
