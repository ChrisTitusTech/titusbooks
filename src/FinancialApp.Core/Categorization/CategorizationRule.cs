namespace FinancialApp.Core.Categorization;

public sealed record CategorizationRule
{
    public required Guid Id { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string Name { get; init; }

    public string MatchField { get; init; } = "description";

    public required CategorizationRuleOperator MatchOperator { get; init; }

    public required string MatchValue { get; init; }

    public required Guid TargetAccountId { get; init; }

    public int Priority { get; init; } = 100;

    public bool IsActive { get; init; } = true;
}
