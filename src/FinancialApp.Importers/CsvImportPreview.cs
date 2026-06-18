namespace FinancialApp.Importers;

public sealed record CsvImportPreview(
    IReadOnlyList<string> Headers,
    IReadOnlyList<CsvImportRow> Rows)
{
    public int ValidCount => Rows.Count(row => row.IsValid);

    public int ErrorCount => Rows.Count(row => !row.IsValid);
}
