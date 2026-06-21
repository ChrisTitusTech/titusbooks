using Dapper;
using FinancialApp.Core.Categorization;
using FinancialApp.Data.Database;

namespace FinancialApp.Data.Repositories;

public sealed class PostgresCategorizationRuleRepository : ICategorizationRuleRepository
{
    private readonly DatabaseConnectionFactory connectionFactory;

    public PostgresCategorizationRuleRepository(DatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task AddAsync(
        CategorizationRule rule,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO categorization_rules (
                id,
                organization_id,
                name,
                match_field,
                match_operator,
                match_value,
                target_account_id,
                priority,
                is_active
            )
            VALUES (
                @Id,
                @OrganizationId,
                @Name,
                @MatchField,
                @MatchOperator,
                @MatchValue,
                @TargetAccountId,
                @Priority,
                @IsActive
            )
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                rule.Id,
                rule.OrganizationId,
                rule.Name,
                rule.MatchField,
                MatchOperator = CategorizationRuleOperatorNames.ToStorageValue(rule.MatchOperator),
                rule.MatchValue,
                rule.TargetAccountId,
                rule.Priority,
                rule.IsActive
            },
            cancellationToken: cancellationToken));
    }

    public Task<IReadOnlyList<CategorizationRule>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return ListAsync(organizationId, activeOnly: false, cancellationToken);
    }

    public Task<IReadOnlyList<CategorizationRule>> ListActiveAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return ListAsync(organizationId, activeOnly: true, cancellationToken);
    }

    private async Task<IReadOnlyList<CategorizationRule>> ListAsync(
        Guid organizationId,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id,
                organization_id AS OrganizationId,
                name,
                match_field AS MatchField,
                match_operator AS MatchOperator,
                match_value AS MatchValue,
                target_account_id AS TargetAccountId,
                priority,
                is_active AS IsActive
            FROM categorization_rules
            WHERE organization_id = @OrganizationId
              AND (@ActiveOnly = FALSE OR is_active = TRUE)
            ORDER BY priority, name, id
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CategorizationRuleRow>(new CommandDefinition(
            sql,
            new { OrganizationId = organizationId, ActiveOnly = activeOnly },
            cancellationToken: cancellationToken));

        return rows.Select(row => row.ToRule()).ToList();
    }

    private sealed record CategorizationRuleRow(
        Guid Id,
        Guid OrganizationId,
        string Name,
        string MatchField,
        string MatchOperator,
        string MatchValue,
        Guid TargetAccountId,
        int Priority,
        bool IsActive)
    {
        public CategorizationRule ToRule()
        {
            if (!CategorizationRuleOperatorNames.TryParse(MatchOperator, out var matchOperator))
            {
                throw new InvalidOperationException(
                    $"Unknown categorization rule operator '{MatchOperator}'.");
            }

            return new CategorizationRule
            {
                Id = Id,
                OrganizationId = OrganizationId,
                Name = Name,
                MatchField = MatchField,
                MatchOperator = matchOperator,
                MatchValue = MatchValue,
                TargetAccountId = TargetAccountId,
                Priority = Priority,
                IsActive = IsActive
            };
        }
    }
}
