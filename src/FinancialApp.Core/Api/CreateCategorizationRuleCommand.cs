namespace FinancialApp.Core.Api;

public sealed record CreateCategorizationRuleCommand(
    string Name,
    string MatchField,
    string MatchOperator,
    string MatchValue,
    Guid TargetAccountId,
    int Priority = 100);
