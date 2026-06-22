namespace FinancialApp.Core.Api;

public sealed record CategorizationRuleSummary(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string MatchField,
    string MatchOperator,
    string MatchValue,
    Guid TargetAccountId,
    int Priority,
    bool IsActive);
