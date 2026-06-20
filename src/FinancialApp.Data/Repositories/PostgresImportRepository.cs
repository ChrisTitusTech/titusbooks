using Dapper;
using FinancialApp.Core.Imports;
using FinancialApp.Data.Database;

namespace FinancialApp.Data.Repositories;

public sealed class PostgresImportRepository : IImportRepository
{
    private readonly DatabaseConnectionFactory connectionFactory;

    public PostgresImportRepository(DatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlySet<string>> FindExistingFingerprintsAsync(
        Guid organizationId,
        IReadOnlyCollection<string> fingerprints,
        CancellationToken cancellationToken = default)
    {
        if (fingerprints.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        const string sql = """
            SELECT fingerprint
            FROM imported_transactions
            WHERE organization_id = @OrganizationId
              AND fingerprint = ANY(@Fingerprints)
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                OrganizationId = organizationId,
                Fingerprints = fingerprints.ToArray()
            },
            cancellationToken: cancellationToken);
        var existing = await connection.QueryAsync<string>(command);

        return existing.ToHashSet(StringComparer.Ordinal);
    }

    public async Task AddBatchAsync(
        ImportBatch batch,
        IReadOnlyCollection<ImportedTransaction> transactions,
        CancellationToken cancellationToken = default)
    {
        const string insertBatchSql = """
            INSERT INTO import_batches (
                id,
                organization_id,
                source,
                file_name,
                file_hash,
                imported_at,
                raw_metadata
            )
            VALUES (
                @Id,
                @OrganizationId,
                @Source,
                @FileName,
                @FileHash,
                @ImportedAt,
                CAST(@RawMetadataJson AS jsonb)
            )
            """;

        const string insertTransactionSql = """
            INSERT INTO imported_transactions (
                id,
                organization_id,
                import_batch_id,
                source,
                source_transaction_id,
                posted_date,
                description,
                raw_description,
                amount,
                balance,
                currency,
                status,
                fingerprint,
                raw_payload
            )
            VALUES (
                @Id,
                @OrganizationId,
                @ImportBatchId,
                @Source,
                @SourceTransactionId,
                @PostedDate,
                @Description,
                @RawDescription,
                @Amount,
                @Balance,
                @Currency,
                @Status,
                @Fingerprint,
                CAST(@RawPayloadJson AS jsonb)
            )
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var batchCommand = new CommandDefinition(
                insertBatchSql,
                new
                {
                    batch.Id,
                    batch.OrganizationId,
                    batch.Source,
                    batch.FileName,
                    batch.FileHash,
                    batch.ImportedAt,
                    batch.RawMetadataJson
                },
                transaction,
                cancellationToken: cancellationToken);
            await connection.ExecuteAsync(batchCommand);

            foreach (var importedTransaction in transactions)
            {
                var transactionCommand = new CommandDefinition(
                    insertTransactionSql,
                    new
                    {
                        importedTransaction.Id,
                        importedTransaction.OrganizationId,
                        importedTransaction.ImportBatchId,
                        importedTransaction.Source,
                        importedTransaction.SourceTransactionId,
                        PostedDate = importedTransaction.PostedDate.ToDateTime(TimeOnly.MinValue),
                        importedTransaction.Description,
                        importedTransaction.RawDescription,
                        importedTransaction.Amount,
                        importedTransaction.Balance,
                        importedTransaction.Currency,
                        Status = importedTransaction.Status.ToString().ToLowerInvariant(),
                        importedTransaction.Fingerprint,
                        importedTransaction.RawPayloadJson
                    },
                    transaction,
                    cancellationToken: cancellationToken);
                await connection.ExecuteAsync(transactionCommand);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<ImportedTransaction>> ListTransactionsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                organization_id AS OrganizationId,
                import_batch_id AS ImportBatchId,
                source,
                source_transaction_id AS SourceTransactionId,
                posted_date AS PostedDate,
                description,
                raw_description AS RawDescription,
                amount,
                balance,
                currency,
                status,
                fingerprint,
                raw_payload::text AS RawPayloadJson
            FROM imported_transactions
            WHERE organization_id = @OrganizationId
            ORDER BY posted_date DESC, created_at DESC
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { OrganizationId = organizationId },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<ImportedTransactionRow>(command);
        return rows.Select(row => row.ToImportedTransaction()).ToList();
    }

    private sealed record ImportedTransactionRow(
        Guid Id,
        Guid OrganizationId,
        Guid? ImportBatchId,
        string Source,
        string? SourceTransactionId,
        DateOnly PostedDate,
        string Description,
        string? RawDescription,
        decimal Amount,
        decimal? Balance,
        string Currency,
        string Status,
        string Fingerprint,
        string? RawPayloadJson)
    {
        public ImportedTransaction ToImportedTransaction()
        {
            return new ImportedTransaction
            {
                Id = Id,
                OrganizationId = OrganizationId,
                ImportBatchId = ImportBatchId,
                Source = Source,
                SourceTransactionId = SourceTransactionId,
                PostedDate = PostedDate,
                Description = Description,
                RawDescription = RawDescription,
                Amount = Amount,
                Balance = Balance,
                Currency = Currency,
                Status = Enum.Parse<ImportedTransactionStatus>(Status, ignoreCase: true),
                Fingerprint = Fingerprint,
                RawPayloadJson = RawPayloadJson
            };
        }
    }
}
