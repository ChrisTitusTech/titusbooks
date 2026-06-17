using Dapper;
using FinancialApp.Core.Accounting;
using FinancialApp.Data.Database;

namespace FinancialApp.Data.Repositories;

public sealed class PostgresJournalEntryRepository : IJournalEntryRepository
{
    private readonly DatabaseConnectionFactory connectionFactory;

    public PostgresJournalEntryRepository(DatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task AddAsync(JournalEntry journalEntry, CancellationToken cancellationToken = default)
    {
        journalEntry.EnsureBalanced();

        const string insertJournalEntrySql = """
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

        const string insertJournalLineSql = """
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

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(insertJournalEntrySql, new
            {
                journalEntry.Id,
                journalEntry.OrganizationId,
                EntryDate = ToDatabaseDate(journalEntry.EntryDate),
                journalEntry.Memo,
                journalEntry.SourceImportedTransactionId,
                journalEntry.IsVoid,
                journalEntry.VoidedAt,
                journalEntry.CreatedAt,
                journalEntry.UpdatedAt
            }, transaction);

            foreach (var line in journalEntry.Lines)
            {
                await connection.ExecuteAsync(insertJournalLineSql, new
                {
                    line.Id,
                    line.JournalEntryId,
                    line.AccountId,
                    line.Debit,
                    line.Credit,
                    line.Memo
                }, transaction);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<AccountRegisterEntry>> ListRegisterAsync(
        Guid organizationId,
        Guid accountId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                je.id AS JournalEntryId,
                je.entry_date AS EntryDate,
                je.memo AS Memo,
                account_line.debit AS Debit,
                account_line.credit AS Credit,
                COALESCE(string_agg(other_accounts.name, ', ' ORDER BY other_accounts.name), '') AS OtherAccounts
            FROM journal_entries je
            INNER JOIN journal_lines account_line
                ON account_line.journal_entry_id = je.id
                AND account_line.account_id = @AccountId
            LEFT JOIN journal_lines other_lines
                ON other_lines.journal_entry_id = je.id
                AND other_lines.account_id <> @AccountId
            LEFT JOIN accounts other_accounts
                ON other_accounts.id = other_lines.account_id
            WHERE je.organization_id = @OrganizationId
              AND je.is_void = false
              AND (CAST(@StartDate AS date) IS NULL OR je.entry_date >= CAST(@StartDate AS date))
              AND (CAST(@EndDate AS date) IS NULL OR je.entry_date <= CAST(@EndDate AS date))
            GROUP BY
                je.id,
                je.entry_date,
                je.created_at,
                je.memo,
                account_line.debit,
                account_line.credit
            ORDER BY je.entry_date DESC, je.created_at DESC
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AccountRegisterEntryRow>(sql, new
        {
            OrganizationId = organizationId,
            AccountId = accountId,
            StartDate = ToDatabaseDate(startDate),
            EndDate = ToDatabaseDate(endDate)
        });

        return rows.Select(row => row.ToRegisterEntry()).ToList();
    }

    private static DateTime ToDatabaseDate(DateOnly value)
    {
        return value.ToDateTime(TimeOnly.MinValue);
    }

    private static DateTime? ToDatabaseDate(DateOnly? value)
    {
        return value?.ToDateTime(TimeOnly.MinValue);
    }

    private sealed record AccountRegisterEntryRow(
        Guid JournalEntryId,
        DateOnly EntryDate,
        string? Memo,
        decimal Debit,
        decimal Credit,
        string OtherAccounts)
    {
        public AccountRegisterEntry ToRegisterEntry()
        {
            return new AccountRegisterEntry(
                JournalEntryId,
                EntryDate,
                Memo,
                Debit,
                Credit,
                OtherAccounts);
        }
    }
}
