using Dapper;
using FinancialApp.Core.Organizations;
using FinancialApp.Data.Database;

namespace FinancialApp.Data.Repositories;

public sealed class PostgresOrganizationRepository
{
    private readonly DatabaseConnectionFactory connectionFactory;

    public PostgresOrganizationRepository(DatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO organizations (
                id,
                name,
                base_currency,
                fiscal_year_start_month,
                accounting_method,
                created_at,
                updated_at
            )
            VALUES (
                @Id,
                @Name,
                @BaseCurrency,
                @FiscalYearStartMonth,
                @AccountingMethod,
                @CreatedAt,
                @UpdatedAt
            )
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, organization);
    }
}
