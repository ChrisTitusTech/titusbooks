namespace FinancialApp.Core.Categorization;

public static class CategorizationRuleOperatorNames
{
    public static string ToStorageValue(CategorizationRuleOperator matchOperator)
    {
        return matchOperator switch
        {
            CategorizationRuleOperator.Contains => "contains",
            CategorizationRuleOperator.Equals => "equals",
            CategorizationRuleOperator.StartsWith => "starts_with",
            CategorizationRuleOperator.EndsWith => "ends_with",
            CategorizationRuleOperator.Regex => "regex",
            CategorizationRuleOperator.AmountEquals => "amount_equals",
            CategorizationRuleOperator.AmountBetween => "amount_between",
            _ => throw new ArgumentOutOfRangeException(nameof(matchOperator), matchOperator, null)
        };
    }

    public static bool TryParse(string value, out CategorizationRuleOperator matchOperator)
    {
        matchOperator = value.Trim().ToLowerInvariant() switch
        {
            "contains" => CategorizationRuleOperator.Contains,
            "equals" => CategorizationRuleOperator.Equals,
            "starts_with" => CategorizationRuleOperator.StartsWith,
            "ends_with" => CategorizationRuleOperator.EndsWith,
            "regex" => CategorizationRuleOperator.Regex,
            "amount_equals" => CategorizationRuleOperator.AmountEquals,
            "amount_between" => CategorizationRuleOperator.AmountBetween,
            _ => default
        };

        return value.Trim().ToLowerInvariant() is
            "contains" or
            "equals" or
            "starts_with" or
            "ends_with" or
            "regex" or
            "amount_equals" or
            "amount_between";
    }
}
