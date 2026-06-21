using FinancialApp.Core.Categorization;
using FinancialApp.Core.Imports;

namespace FinancialApp.Core.Tests.Categorization;

public sealed class CategorizationRuleEngineTests
{
    [Fact]
    public void FindMatch_UsesLowestPriorityNumber()
    {
        var organizationId = Guid.NewGuid();
        var transaction = CreateTransaction(organizationId, "ACME OFFICE STORE", -42m);
        var lowerPriorityRule = CreateRule(
            organizationId,
            "Office",
            CategorizationRuleOperator.Contains,
            "office",
            priority: 200);
        var higherPriorityRule = CreateRule(
            organizationId,
            "Acme",
            CategorizationRuleOperator.StartsWith,
            "acme",
            priority: 10);

        var match = new CategorizationRuleEngine().FindMatch(
            transaction,
            [lowerPriorityRule, higherPriorityRule]);

        Assert.Equal(higherPriorityRule.Id, match?.Id);
    }

    [Theory]
    [InlineData(CategorizationRuleOperator.Contains, "office", true)]
    [InlineData(CategorizationRuleOperator.Equals, "ACME OFFICE STORE", true)]
    [InlineData(CategorizationRuleOperator.StartsWith, "acme", true)]
    [InlineData(CategorizationRuleOperator.EndsWith, "store", true)]
    [InlineData(CategorizationRuleOperator.Regex, "^acme\\s+office", true)]
    [InlineData(CategorizationRuleOperator.Contains, "travel", false)]
    public void IsMatch_MatchesDescriptionOperators(
        CategorizationRuleOperator matchOperator,
        string matchValue,
        bool expected)
    {
        var organizationId = Guid.NewGuid();
        var transaction = CreateTransaction(organizationId, "ACME OFFICE STORE", -42m);
        var rule = CreateRule(organizationId, "Rule", matchOperator, matchValue);

        var result = new CategorizationRuleEngine().IsMatch(transaction, rule);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(CategorizationRuleOperator.AmountEquals, "-42.00", true)]
    [InlineData(CategorizationRuleOperator.AmountBetween, "-50..-40", true)]
    [InlineData(CategorizationRuleOperator.AmountBetween, "-40,-50", true)]
    [InlineData(CategorizationRuleOperator.AmountEquals, "42.00", false)]
    public void IsMatch_MatchesAmountOperators(
        CategorizationRuleOperator matchOperator,
        string matchValue,
        bool expected)
    {
        var organizationId = Guid.NewGuid();
        var transaction = CreateTransaction(organizationId, "Purchase", -42m);
        var rule = CreateRule(
            organizationId,
            "Amount",
            matchOperator,
            matchValue,
            matchField: "amount");

        var result = new CategorizationRuleEngine().IsMatch(transaction, rule);

        Assert.Equal(expected, result);
    }

    private static ImportedTransaction CreateTransaction(
        Guid organizationId,
        string description,
        decimal amount)
    {
        return new ImportedTransaction
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Source = "Generic CSV",
            PostedDate = new DateOnly(2026, 6, 21),
            Description = description,
            Amount = amount,
            Fingerprint = Guid.NewGuid().ToString("N")
        };
    }

    private static CategorizationRule CreateRule(
        Guid organizationId,
        string name,
        CategorizationRuleOperator matchOperator,
        string matchValue,
        int priority = 100,
        string matchField = "description")
    {
        return new CategorizationRule
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            MatchField = matchField,
            MatchOperator = matchOperator,
            MatchValue = matchValue,
            TargetAccountId = Guid.NewGuid(),
            Priority = priority
        };
    }
}
