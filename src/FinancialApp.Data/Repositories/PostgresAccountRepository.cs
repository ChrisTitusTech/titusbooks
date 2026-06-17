using Dapper;
using FinancialApp.Core.Accounting;
using FinancialApp.Data.Database;

namespace FinancialApp.Data.Repositories;

public sealed class PostgresAccountRepository : IAccountRepository
{
    private readonly DatabaseConnectionFactory connectionFactory;

    public PostgresAccountRepository(DatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<Account?> FindByNameAsync(
        Guid organizationId,
        string name,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                organization_id AS OrganizationId,
                name,
                account_type AS AccountType,
                account_subtype AS AccountSubtype,
                currency,
                parent_account_id AS ParentAccountId,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM accounts
            WHERE organization_id = @OrganizationId
              AND name = @Name
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AccountRow>(sql, new { OrganizationId = organizationId, Name = name });
        return row?.ToAccount();
    }

    public async Task<Account?> GetByIdAsync(
        Guid organizationId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                organization_id AS OrganizationId,
                name,
                account_type AS AccountType,
                account_subtype AS AccountSubtype,
                currency,
                parent_account_id AS ParentAccountId,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM accounts
            WHERE organization_id = @OrganizationId
              AND id = @AccountId
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AccountRow>(
            sql,
            new { OrganizationId = organizationId, AccountId = accountId });
        return row?.ToAccount();
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO accounts (
                id,
                organization_id,
                name,
                account_type,
                account_subtype,
                currency,
                parent_account_id,
                is_active,
                created_at,
                updated_at
            )
            VALUES (
                @Id,
                @OrganizationId,
                @Name,
                @AccountType,
                @AccountSubtype,
                @Currency,
                @ParentAccountId,
                @IsActive,
                @CreatedAt,
                @UpdatedAt
            )
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new
        {
            account.Id,
            account.OrganizationId,
            account.Name,
            AccountType = account.AccountType.ToString(),
            account.AccountSubtype,
            account.Currency,
            account.ParentAccountId,
            account.IsActive,
            account.CreatedAt,
            account.UpdatedAt
        });
    }

    public async Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE accounts
            SET name = @Name,
                account_subtype = @AccountSubtype,
                updated_at = @UpdatedAt
            WHERE organization_id = @OrganizationId
              AND id = @Id
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new
        {
            account.Id,
            account.OrganizationId,
            account.Name,
            account.AccountSubtype,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task DeactivateAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE accounts
            SET is_active = false,
                updated_at = @UpdatedAt
            WHERE organization_id = @OrganizationId
              AND id = @AccountId
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new
        {
            OrganizationId = organizationId,
            AccountId = accountId,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task<IReadOnlyList<Account>> ListByOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id,
                organization_id AS OrganizationId,
                name,
                account_type AS AccountType,
                account_subtype AS AccountSubtype,
                currency,
                parent_account_id AS ParentAccountId,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM accounts
            WHERE organization_id = @OrganizationId
            ORDER BY account_type, name
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AccountRow>(sql, new { OrganizationId = organizationId });
        return rows.Select(row => row.ToAccount()).ToList();
    }

    private sealed record AccountRow(
        Guid Id,
        Guid OrganizationId,
        string Name,
        string AccountType,
        string? AccountSubtype,
        string Currency,
        Guid? ParentAccountId,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public Account ToAccount()
        {
            return new Account
            {
                Id = Id,
                OrganizationId = OrganizationId,
                Name = Name,
                AccountType = Enum.Parse<AccountType>(AccountType),
                AccountSubtype = AccountSubtype,
                Currency = Currency,
                ParentAccountId = ParentAccountId,
                IsActive = IsActive,
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
