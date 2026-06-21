using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinancialApp.Core.Categorization;
using FinancialApp.Core.Imports;

namespace FinancialApp.Importers;

public sealed class CsvImportService
{
    private readonly GenericCsvParser parser;
    private readonly PayPalCsvParser payPalParser;
    private readonly IImportRepository repository;
    private readonly ICategorizationRuleRepository? categorizationRuleRepository;
    private readonly CategorizationRuleEngine categorizationRuleEngine;

    public CsvImportService(
        GenericCsvParser parser,
        IImportRepository repository,
        PayPalCsvParser? payPalParser = null,
        ICategorizationRuleRepository? categorizationRuleRepository = null,
        CategorizationRuleEngine? categorizationRuleEngine = null)
    {
        this.parser = parser;
        this.repository = repository;
        this.payPalParser = payPalParser ?? new PayPalCsvParser();
        this.categorizationRuleRepository = categorizationRuleRepository;
        this.categorizationRuleEngine = categorizationRuleEngine ?? new CategorizationRuleEngine();
    }

    public CsvImportPreview Preview(CsvImportRequest request)
    {
        return Parse(request);
    }

    public async Task<CsvImportResult> ImportAsync(
        CsvImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var preview = Parse(request);
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
            fingerprints,
            cancellationToken);

        var seen = new HashSet<string>(existing, StringComparer.Ordinal);
        var uniqueTransactions = new List<ImportedTransaction>();
        var duplicateCount = 0;

        foreach (var transaction in validTransactions)
        {
            if (!seen.Add(transaction.Fingerprint))
            {
                duplicateCount++;
                continue;
            }

            uniqueTransactions.Add(transaction);
        }

        if (uniqueTransactions.Count == 0)
        {
            return new CsvImportResult(
                null,
                0,
                0,
                duplicateCount,
                preview.ErrorCount,
                preview.SkippedCount);
        }

        var batchId = Guid.NewGuid();
        var categorizedTransactions = await ApplyCategorizationRulesAsync(
            request.OrganizationId,
            uniqueTransactions,
            cancellationToken);
        var pendingTransactions = categorizedTransactions
            .Select(transaction => transaction with { ImportBatchId = batchId })
            .ToList();
        await repository.AddBatchAsync(
            CreateBatch(request, preview, batchId),
            pendingTransactions,
            cancellationToken);

        return new CsvImportResult(
            batchId,
            pendingTransactions.Count(transaction =>
                transaction.Status == ImportedTransactionStatus.Pending),
            pendingTransactions.Count(transaction =>
                transaction.Status == ImportedTransactionStatus.Categorized),
            duplicateCount,
            preview.ErrorCount,
            preview.SkippedCount);
    }

    private async Task<IReadOnlyList<ImportedTransaction>> ApplyCategorizationRulesAsync(
        Guid organizationId,
        IReadOnlyList<ImportedTransaction> transactions,
        CancellationToken cancellationToken)
    {
        if (categorizationRuleRepository is null)
        {
            return transactions;
        }

        var rules = await categorizationRuleRepository.ListActiveAsync(
            organizationId,
            cancellationToken);
        if (rules.Count == 0)
        {
            return transactions;
        }

        return transactions
            .Select(transaction =>
            {
                var match = categorizationRuleEngine.FindMatch(transaction, rules);
                return match is null
                    ? transaction
                    : transaction with
                    {
                        CategoryAccountId = match.TargetAccountId,
                        MatchedRuleId = match.Id,
                        Status = ImportedTransactionStatus.Categorized
                    };
            })
            .ToList();
    }

    private CsvImportPreview Parse(CsvImportRequest request)
    {
        return string.Equals(
            request.Source,
            CsvImportProfiles.PayPalName,
            StringComparison.OrdinalIgnoreCase)
            ? payPalParser.Parse(request)
            : parser.Parse(request);
    }

    private static ImportBatch CreateBatch(
        CsvImportRequest request,
        CsvImportPreview preview,
        Guid batchId)
    {
        return new ImportBatch
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
                RowCount = preview.Rows.Count,
                preview.SkippedCount
            })
        };
    }
}
