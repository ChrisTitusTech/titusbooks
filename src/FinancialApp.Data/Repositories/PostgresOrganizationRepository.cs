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
        var rows = await connection.QueryAsync<OrganizationRow>(sql);
        return rows.Select(row => row.ToOrganization()).ToList();
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
        var row = await connection.QuerySingleOrDefaultAsync<OrganizationRow>(sql, new { Id = id });
        return row?.ToOrganization();
    }

    private sealed record OrganizationRow(
        Guid Id,
        string Name,
        string BaseCurrency,
        int FiscalYearStartMonth,
        string AccountingMethod,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public Organization ToOrganization()
        {
            return new Organization
            {
                Id = Id,
                Name = Name,
                BaseCurrency = BaseCurrency,
                FiscalYearStartMonth = FiscalYearStartMonth,
                AccountingMethod = AccountingMethod,
                CreatedAt = ToUtcOffset(CreatedAt),
                UpdatedAt = ToUtcOffset(UpdatedAt)
            };
        }
    }

    private static DateTimeOffset ToUtcOffset(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
