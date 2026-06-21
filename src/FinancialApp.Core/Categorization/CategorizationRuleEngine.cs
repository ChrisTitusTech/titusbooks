using System.Globalization;
using System.Text.RegularExpressions;
using FinancialApp.Core.Imports;

namespace FinancialApp.Core.Categorization;

public sealed class CategorizationRuleEngine
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    public CategorizationRule? FindMatch(
        ImportedTransaction transaction,
        IEnumerable<CategorizationRule> rules)
    {
        return rules
            .Where(rule => rule.IsActive && rule.OrganizationId == transaction.OrganizationId)
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Id)
            .FirstOrDefault(rule => IsMatch(transaction, rule));
    }

    public bool IsMatch(ImportedTransaction transaction, CategorizationRule rule)
    {
        return rule.MatchOperator switch
        {
            CategorizationRuleOperator.AmountEquals => MatchAmountEquals(transaction.Amount, rule.MatchValue),
            CategorizationRuleOperator.AmountBetween => MatchAmountBetween(transaction.Amount, rule.MatchValue),
            _ => GetTextValue(transaction, rule.MatchField) is { } textValue
                && MatchText(textValue, rule)
        };
    }

    private static string? GetTextValue(ImportedTransaction transaction, string matchField)
    {
        return matchField.Trim().ToLowerInvariant() switch
        {
            "description" => transaction.Description,
            "raw_description" => transaction.RawDescription ?? string.Empty,
            "source" => transaction.Source,
            "source_type" => transaction.SourceType ?? string.Empty,
            "source_status" => transaction.SourceStatus ?? string.Empty,
            "kind" => ImportedTransactionKindNames.ToStorageValue(transaction.Kind),
            "currency" => transaction.Currency,
            _ => null
        };
    }

    private static bool MatchText(string candidate, CategorizationRule rule)
    {
        var matchValue = rule.MatchValue.Trim();
        return rule.MatchOperator switch
        {
            CategorizationRuleOperator.Contains => candidate.Contains(
                matchValue,
                StringComparison.OrdinalIgnoreCase),
            CategorizationRuleOperator.Equals => string.Equals(
                candidate.Trim(),
                matchValue,
                StringComparison.OrdinalIgnoreCase),
            CategorizationRuleOperator.StartsWith => candidate.TrimStart().StartsWith(
                matchValue,
                StringComparison.OrdinalIgnoreCase),
            CategorizationRuleOperator.EndsWith => candidate.TrimEnd().EndsWith(
                matchValue,
                StringComparison.OrdinalIgnoreCase),
            CategorizationRuleOperator.Regex => MatchRegex(candidate, matchValue),
            _ => false
        };
    }

    private static bool MatchRegex(string candidate, string matchValue)
    {
        try
        {
            return Regex.IsMatch(
                candidate,
                matchValue,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                RegexTimeout);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool MatchAmountEquals(decimal amount, string matchValue)
    {
        return decimal.TryParse(
            matchValue,
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var expected)
            && amount == expected;
    }

    private static bool MatchAmountBetween(decimal amount, string matchValue)
    {
        var bounds = matchValue.Split(
            ["..", ","],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return bounds.Length == 2
            && decimal.TryParse(bounds[0], NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var first)
            && decimal.TryParse(bounds[1], NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var second)
            && amount >= Math.Min(first, second)
            && amount <= Math.Max(first, second);
    }
}
