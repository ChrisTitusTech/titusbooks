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
                journalEntry.EntryDate,
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
}
