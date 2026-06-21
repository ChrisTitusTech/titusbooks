namespace FinancialApp.Api.Categorization;

public sealed record CreateCategorizationRuleRequest(
    string Name,
    string MatchField,
    string MatchOperator,
    string MatchValue,
    Guid TargetAccountId,
    int Priority = 100);
