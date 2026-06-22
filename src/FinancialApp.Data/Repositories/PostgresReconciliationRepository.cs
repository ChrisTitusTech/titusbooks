using Dapper;
using FinancialApp.Core.Reconciliation;
using FinancialApp.Data.Database;

namespace FinancialApp.Data.Repositories;

public sealed class PostgresReconciliationRepository : IReconciliationRepository
{
    private readonly DatabaseConnectionFactory connectionFactory;

    public PostgresReconciliationRepository(DatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ReconciliationTransaction>> ListTransactionsAsync(
        Guid organizationId,
        Guid accountId,
        DateOnly statementEndDate,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                account_line.id AS JournalLineId,
                je.id AS JournalEntryId,
                je.entry_date AS EntryDate,
                je.memo AS Memo,
                account_line.debit AS Debit,
                account_line.credit AS Credit,
                COALESCE(string_agg(other_accounts.name, ', ' ORDER BY other_accounts.name), '') AS OtherAccounts,
                account_line.reconciliation_id AS ReconciliationId
            FROM journal_entries je
            INNER JOIN journal_lines account_line
                ON account_line.journal_entry_id = je.id
                AND account_line.account_id = @AccountId
            LEFT JOIN journal_lines other_lines
                ON other_lines.journal_entry_id = je.id
                AND other_lines.id <> account_line.id
            LEFT JOIN accounts other_accounts
                ON other_accounts.id = other_lines.account_id
            WHERE je.organization_id = @OrganizationId
              AND je.is_void = false
              AND je.entry_date <= @StatementEndDate
            GROUP BY
                account_line.id,
                je.id,
                je.entry_date,
                je.created_at,
                je.memo,
                account_line.debit,
                account_line.credit,
                account_line.reconciliation_id
            ORDER BY je.entry_date, je.created_at, account_line.id
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ReconciliationTransaction>(new CommandDefinition(
            sql,
            new
            {
                OrganizationId = organizationId,
                AccountId = accountId,
                StatementEndDate = statementEndDate.ToDateTime(TimeOnly.MinValue)
            },
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task CompleteAsync(
        Reconciliation reconciliation,
        IReadOnlyCollection<Guid> journalLineIds,
        CancellationToken cancellationToken = default)
    {
        const string insertSql = """
            INSERT INTO reconciliations (
                id,
                organization_id,
                account_id,
                statement_end_date,
                statement_end_balance,
                completed_at,
                created_at
            )
            VALUES (
                @Id,
                @OrganizationId,
                @AccountId,
                @StatementEndDate,
                @StatementEndBalance,
                @CompletedAt,
                @CreatedAt
            )
            """;
        const string updateSql = """
            UPDATE journal_lines AS line
            SET reconciliation_id = @ReconciliationId
            FROM journal_entries AS entry
            WHERE line.journal_entry_id = entry.id
              AND line.id = ANY(@JournalLineIds)
              AND line.account_id = @AccountId
              AND line.reconciliation_id IS NULL
              AND entry.organization_id = @OrganizationId
              AND entry.is_void = false
              AND entry.entry_date <= @StatementEndDate
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new
                {
                    reconciliation.Id,
                    reconciliation.OrganizationId,
                    reconciliation.AccountId,
                    StatementEndDate = reconciliation.StatementEndDate.ToDateTime(TimeOnly.MinValue),
                    reconciliation.StatementEndBalance,
                    reconciliation.CompletedAt,
                    reconciliation.CreatedAt
                },
                transaction,
                cancellationToken: cancellationToken));

            if (journalLineIds.Count > 0)
            {
                var updatedCount = await connection.ExecuteAsync(new CommandDefinition(
                    updateSql,
                    new
                    {
                        ReconciliationId = reconciliation.Id,
                        JournalLineIds = journalLineIds.ToArray(),
                        reconciliation.AccountId,
                        reconciliation.OrganizationId,
                        StatementEndDate = reconciliation.StatementEndDate.ToDateTime(TimeOnly.MinValue)
                    },
                    transaction,
                    cancellationToken: cancellationToken));
                if (updatedCount != journalLineIds.Count)
                {
                    throw new ReconciliationException(
                        "One or more selected transactions changed before reconciliation completed.");
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
