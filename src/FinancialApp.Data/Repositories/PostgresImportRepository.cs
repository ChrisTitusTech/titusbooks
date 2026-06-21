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
                reference_transaction_id,
                source_type,
                source_status,
                posted_date,
                posted_time,
                source_time_zone,
                description,
                raw_description,
                amount,
                balance,
                gross_amount,
                fee_amount,
                net_amount,
                transaction_kind,
                currency,
                status,
                category_account_id,
                matched_rule_id,
                fingerprint,
                raw_payload
            )
            VALUES (
                @Id,
                @OrganizationId,
                @ImportBatchId,
                @Source,
                @SourceTransactionId,
                @ReferenceTransactionId,
                @SourceType,
                @SourceStatus,
                @PostedDate,
                @PostedTime,
                @SourceTimeZone,
                @Description,
                @RawDescription,
                @Amount,
                @Balance,
                @GrossAmount,
                @FeeAmount,
                @NetAmount,
                @TransactionKind,
                @Currency,
                @Status,
                @CategoryAccountId,
                @MatchedRuleId,
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
                        importedTransaction.ReferenceTransactionId,
                        importedTransaction.SourceType,
                        importedTransaction.SourceStatus,
                        PostedDate = importedTransaction.PostedDate.ToDateTime(TimeOnly.MinValue),
                        PostedTime = importedTransaction.PostedTime?.ToTimeSpan(),
                        importedTransaction.SourceTimeZone,
                        importedTransaction.Description,
                        importedTransaction.RawDescription,
                        importedTransaction.Amount,
                        importedTransaction.Balance,
                        importedTransaction.GrossAmount,
                        importedTransaction.FeeAmount,
                        importedTransaction.NetAmount,
                        TransactionKind = ImportedTransactionKindNames.ToStorageValue(
                            importedTransaction.Kind),
                        importedTransaction.Currency,
                        Status = importedTransaction.Status.ToString().ToLowerInvariant(),
                        importedTransaction.CategoryAccountId,
                        importedTransaction.MatchedRuleId,
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
        ImportedTransactionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                id,
                organization_id AS OrganizationId,
                import_batch_id AS ImportBatchId,
                source,
                source_transaction_id AS SourceTransactionId,
                reference_transaction_id AS ReferenceTransactionId,
                source_type AS SourceType,
                source_status AS SourceStatus,
                posted_date AS PostedDate,
                posted_time AS PostedTime,
                source_time_zone AS SourceTimeZone,
                description,
                raw_description AS RawDescription,
                amount,
                balance,
                gross_amount AS GrossAmount,
                fee_amount AS FeeAmount,
                net_amount AS NetAmount,
                transaction_kind AS TransactionKind,
                currency,
                status,
                category_account_id AS CategoryAccountId,
                matched_rule_id AS MatchedRuleId,
                fingerprint,
                raw_payload::text AS RawPayloadJson
            FROM imported_transactions
            WHERE organization_id = @OrganizationId
            """;
        if (status is not null)
        {
            sql += "\n  AND status = @Status";
        }

        sql += "\nORDER BY posted_date DESC, created_at DESC";

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                OrganizationId = organizationId,
                Status = status?.ToString().ToLowerInvariant()
            },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<ImportedTransactionRow>(command);
        return rows.Select(row => row.ToImportedTransaction()).ToList();
    }

    public async Task<bool> CategorizeTransactionsAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> transactionIds,
        Guid categoryAccountId,
        CancellationToken cancellationToken = default)
    {
        if (transactionIds.Count == 0)
        {
            return false;
        }

        const string countSql = """
            SELECT COUNT(*)
            FROM imported_transactions
            WHERE organization_id = @OrganizationId
              AND id = ANY(@TransactionIds)
              AND status IN ('pending', 'categorized')
            """;
        const string updateSql = """
            UPDATE imported_transactions
            SET
                category_account_id = @CategoryAccountId,
                matched_rule_id = NULL,
                status = 'categorized',
                updated_at = now()
            WHERE organization_id = @OrganizationId
              AND id = ANY(@TransactionIds)
              AND status IN ('pending', 'categorized')
            """;

        var distinctIds = transactionIds.Distinct().ToArray();
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var parameters = new
        {
            OrganizationId = organizationId,
            TransactionIds = distinctIds,
            CategoryAccountId = categoryAccountId
        };
        var eligibleCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            countSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken));
        if (eligibleCount != distinctIds.Length)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            updateSql,
            parameters,
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private sealed record ImportedTransactionRow(
        Guid Id,
        Guid OrganizationId,
        Guid? ImportBatchId,
        string Source,
        string? SourceTransactionId,
        string? ReferenceTransactionId,
        string? SourceType,
        string? SourceStatus,
        DateOnly PostedDate,
        TimeOnly? PostedTime,
        string? SourceTimeZone,
        string Description,
        string? RawDescription,
        decimal Amount,
        decimal? Balance,
        decimal? GrossAmount,
        decimal? FeeAmount,
        decimal? NetAmount,
        string TransactionKind,
        string Currency,
        string Status,
        Guid? CategoryAccountId,
        Guid? MatchedRuleId,
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
                ReferenceTransactionId = ReferenceTransactionId,
                SourceType = SourceType,
                SourceStatus = SourceStatus,
                PostedDate = PostedDate,
                PostedTime = PostedTime,
                SourceTimeZone = SourceTimeZone,
                Description = Description,
                RawDescription = RawDescription,
                Amount = Amount,
                Balance = Balance,
                GrossAmount = GrossAmount,
                FeeAmount = FeeAmount,
                NetAmount = NetAmount,
                Kind = ImportedTransactionKindNames.Parse(TransactionKind),
                Currency = Currency,
                Status = Enum.Parse<ImportedTransactionStatus>(Status, ignoreCase: true),
                CategoryAccountId = CategoryAccountId,
                MatchedRuleId = MatchedRuleId,
                Fingerprint = Fingerprint,
                RawPayloadJson = RawPayloadJson
            };
        }
    }
}
