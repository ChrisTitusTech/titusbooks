using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinancialApp.Core.Imports;

namespace FinancialApp.Importers;

public sealed class CsvImportService
{
    private readonly GenericCsvParser parser;
    private readonly IImportRepository repository;

    public CsvImportService(GenericCsvParser parser, IImportRepository repository)
    {
        this.parser = parser;
        this.repository = repository;
    }

    public CsvImportPreview Preview(CsvImportRequest request)
    {
        return parser.Parse(request);
    }

    public async Task<CsvImportResult> ImportAsync(
        CsvImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var preview = parser.Parse(request);
        var validTransactions = preview.Rows
            .Where(row => row.Transaction is not null)
            .Select(row => row.Transaction!)
            .ToList();
        var fingerprints = validTransactions
            .Select(transaction => transaction.Fingerprint)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var existing = await repository.FindExistingFingerprintsAsync(
            request.OrganizationId,
            request.Source,
            fingerprints,
            cancellationToken);

        var seen = new HashSet<string>(existing, StringComparer.Ordinal);
        var pendingTransactions = new List<ImportedTransaction>();
        var duplicateCount = 0;
        var batchId = Guid.NewGuid();

        foreach (var transaction in validTransactions)
        {
            if (!seen.Add(transaction.Fingerprint))
            {
                duplicateCount++;
                continue;
            }

            pendingTransactions.Add(transaction with { ImportBatchId = batchId });
        }

        var batch = new ImportBatch
        {
            Id = batchId,
            OrganizationId = request.OrganizationId,
            Source = request.Source.Trim(),
            FileName = string.IsNullOrWhiteSpace(request.FileName) ? null : request.FileName.Trim(),
            FileHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(request.CsvContent))).ToLowerInvariant(),
            RawMetadataJson = JsonSerializer.Serialize(new
            {
                preview.Headers,
                Mapping = request.Mapping,
                RowCount = preview.Rows.Count
            })
        };

        await repository.AddBatchAsync(batch, pendingTransactions, cancellationToken);

        return new CsvImportResult(
            batch.Id,
            pendingTransactions.Count,
            duplicateCount,
            preview.ErrorCount);
    }
}
