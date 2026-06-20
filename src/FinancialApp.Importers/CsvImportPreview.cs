namespace FinancialApp.Importers;

public sealed record CsvImportPreview(
    IReadOnlyList<string> Headers,
    IReadOnlyList<CsvImportRow> Rows,
    int SkippedCount = 0)
{
    public int ValidCount => Rows.Count(row => row.IsValid);

    public int ErrorCount => Rows.Count(row => !row.IsValid);
}
