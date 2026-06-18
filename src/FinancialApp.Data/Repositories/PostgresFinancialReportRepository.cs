using Dapper;
using FinancialApp.Core.Accounting;
using FinancialApp.Data.Database;
using FinancialApp.Reports;

namespace FinancialApp.Data.Repositories;

public sealed class PostgresFinancialReportRepository : IFinancialReportRepository
{
    private readonly DatabaseConnectionFactory connectionFactory;

    public PostgresFinancialReportRepository(DatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<AccountReportTotal>> ListAccountTotalsAsync(
        Guid organizationId,
        AccountType accountType,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                a.id AS AccountId,
                a.name AS AccountName,
                CASE
                    WHEN a.account_type = 'Income' THEN SUM(jl.credit - jl.debit)
                    ELSE SUM(jl.debit - jl.credit)
                END AS Amount
            FROM accounts a
            INNER JOIN journal_lines jl ON jl.account_id = a.id
            INNER JOIN journal_entries je ON je.id = jl.journal_entry_id
            WHERE a.organization_id = @OrganizationId
              AND je.organization_id = @OrganizationId
              AND a.account_type = @AccountType
              AND je.is_void = false
              AND je.entry_date >= @StartDate
              AND je.entry_date <= @EndDate
            GROUP BY a.id, a.name, a.account_type
            HAVING CASE
                WHEN a.account_type = 'Income' THEN SUM(jl.credit - jl.debit)
                ELSE SUM(jl.debit - jl.credit)
            END <> 0
            ORDER BY a.name
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AccountReportTotal>(sql, new
        {
            OrganizationId = organizationId,
            AccountType = accountType.ToString(),
            StartDate = startDate.ToDateTime(TimeOnly.MinValue),
            EndDate = endDate.ToDateTime(TimeOnly.MinValue)
        });

        return rows.ToList();
    }
}
