using Dapper;
using FinancialApp.Core.Accounting;
using FinancialApp.Core.Imports;
using FinancialApp.Data.Database;

namespace FinancialApp.Data.Repositories;

public sealed class PostgresImportPostingRepository : IImportPostingRepository
{
    private readonly DatabaseConnectionFactory connectionFactory;

    public PostgresImportPostingRepository(DatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ImportedTransaction>> ListForPostingAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> transactionIds,
        CancellationToken cancellationToken = default)
    {
        if (transactionIds.Count == 0)
        {
            return [];
        }

        const string sql = """
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
              AND id = ANY(@TransactionIds)
            ORDER BY posted_date, created_at, id
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ImportedTransactionRow>(new CommandDefinition(
            sql,
            new
            {
                OrganizationId = organizationId,
                TransactionIds = transactionIds.Distinct().ToArray()
            },
            cancellationToken: cancellationToken));

        return rows.Select(row => row.ToImportedTransaction()).ToList();
    }

    public async Task PostAsync(
        Guid organizationId,
        IReadOnlyCollection<JournalEntry> journalEntries,
        CancellationToken cancellationToken = default)
    {
        if (journalEntries.Count == 0)
        {
            throw new ImportPostingException("Select at least one imported transaction.");
        }

        const string lockSql = """
            SELECT id
            FROM imported_transactions
            WHERE organization_id = @OrganizationId
              AND id = ANY(@TransactionIds)
              AND status = 'categorized'
              AND category_account_id IS NOT NULL
            FOR UPDATE
            """;
        const string insertEntrySql = """
            INSERT INTO journal_entries (
                id,
                organization_id,
                entry_date,
                memo,
                source_imported_transaction_id,
                is_void,
                voided_at,
                created_at,
                updated_at
            )
            VALUES (
                @Id,
                @OrganizationId,
                @EntryDate,
                @Memo,
                @SourceImportedTransactionId,
                @IsVoid,
                @VoidedAt,
                @CreatedAt,
                @UpdatedAt
            )
            """;
        const string insertLineSql = """
            INSERT INTO journal_lines (
                id,
                journal_entry_id,
                account_id,
                debit,
                credit,
                memo
            )
            VALUES (
                @Id,
                @JournalEntryId,
                @AccountId,
                @Debit,
                @Credit,
                @Memo
            )
            """;
        const string updateStatusSql = """
            UPDATE imported_transactions
            SET status = 'posted', updated_at = now()
            WHERE organization_id = @OrganizationId
              AND id = ANY(@TransactionIds)
              AND status = 'categorized'
            """;

        var transactionIds = journalEntries
            .Select(entry => entry.SourceImportedTransactionId)
            .OfType<Guid>()
            .Distinct()
            .ToArray();
        if (transactionIds.Length != journalEntries.Count)
        {
            throw new ImportPostingException(
                "Each imported transaction must create exactly one journal entry.");
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var databaseTransaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var eligibleIds = await connection.QueryAsync<Guid>(new CommandDefinition(
                lockSql,
                new { OrganizationId = organizationId, TransactionIds = transactionIds },
                databaseTransaction,
                cancellationToken: cancellationToken));
            if (eligibleIds.Count() != transactionIds.Length)
            {
                throw new ImportPostingException(
                    "One or more imported transactions changed before posting completed.");
            }

            foreach (var entry in journalEntries)
            {
                entry.EnsureBalanced();
                await connection.ExecuteAsync(new CommandDefinition(
                    insertEntrySql,
                    new
                    {
                        entry.Id,
                        entry.OrganizationId,
                        EntryDate = entry.EntryDate.ToDateTime(TimeOnly.MinValue),
                        entry.Memo,
                        entry.SourceImportedTransactionId,
                        entry.IsVoid,
                        entry.VoidedAt,
                        entry.CreatedAt,
                        entry.UpdatedAt
                    },
                    databaseTransaction,
                    cancellationToken: cancellationToken));

                foreach (var line in entry.Lines)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        insertLineSql,
                        new
                        {
                            line.Id,
                            line.JournalEntryId,
                            line.AccountId,
                            line.Debit,
                            line.Credit,
                            line.Memo
                        },
                        databaseTransaction,
                        cancellationToken: cancellationToken));
                }
            }

            var updatedCount = await connection.ExecuteAsync(new CommandDefinition(
                updateStatusSql,
                new { OrganizationId = organizationId, TransactionIds = transactionIds },
                databaseTransaction,
                cancellationToken: cancellationToken));
            if (updatedCount != transactionIds.Length)
            {
                throw new ImportPostingException(
                    "One or more imported transactions changed before posting completed.");
            }

            await databaseTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            throw;
        }
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
            if (!Enum.TryParse<ImportedTransactionStatus>(
                    Status,
                    ignoreCase: true,
                    out var status))
            {
                throw new InvalidOperationException(
                    $"Unknown imported transaction status '{Status}'.");
            }

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
                Status = status,
                CategoryAccountId = CategoryAccountId,
                MatchedRuleId = MatchedRuleId,
                Fingerprint = Fingerprint,
                RawPayloadJson = RawPayloadJson
            };
        }
    }
}
