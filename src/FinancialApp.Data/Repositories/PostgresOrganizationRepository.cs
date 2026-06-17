using Dapper;
using FinancialApp.Core.Organizations;
using FinancialApp.Data.Database;

namespace FinancialApp.Data.Repositories;

public sealed class PostgresOrganizationRepository : IOrganizationRepository
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

    public async Task<IReadOnlyList<Organization>> ListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                name,
                base_currency AS BaseCurrency,
                fiscal_year_start_month AS FiscalYearStartMonth,
                accounting_method AS AccountingMethod,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM organizations
            ORDER BY name
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var organizations = await connection.QueryAsync<Organization>(sql);
        return organizations.ToList();
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                name,
                base_currency AS BaseCurrency,
                fiscal_year_start_month AS FiscalYearStartMonth,
                accounting_method AS AccountingMethod,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM organizations
            WHERE id = @Id
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Organization>(sql, new { Id = id });
    }
}
