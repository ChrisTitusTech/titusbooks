using FinancialApp.Core.Categorization;

namespace FinancialApp.Api.Categorization;

public sealed record CategorizationRuleResponse(
    Guid Id,
    string Name,
    string MatchField,
    string MatchOperator,
    string MatchValue,
    Guid TargetAccountId,
    int Priority,
    bool IsActive)
{
    public static CategorizationRuleResponse FromRule(CategorizationRule rule)
    {
        return new CategorizationRuleResponse(
            rule.Id,
            rule.Name,
            rule.MatchField,
            CategorizationRuleOperatorNames.ToStorageValue(rule.MatchOperator),
            rule.MatchValue,
            rule.TargetAccountId,
            rule.Priority,
            rule.IsActive);
    }
}
