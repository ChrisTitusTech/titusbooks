namespace FinancialApp.Core.Accounting;

public static class DefaultChartOfAccounts
{
    public static IReadOnlyList<AccountTemplate> Templates { get; } =
    [
        new("Checking", AccountType.Asset, "Checking"),
        new("PayPal Balance", AccountType.Asset, "PayPal"),
        new("Cash", AccountType.Asset, "Cash"),
        new("Credit Card Liability", AccountType.Liability, "Credit Card"),
        new("Owner's Equity", AccountType.Equity),
        new("Sales Income", AccountType.Income, "Sales Income"),
        new("Consulting Income", AccountType.Income, "Consulting Income"),
        new("Software Subscriptions", AccountType.Expense),
        new("Office Supplies", AccountType.Expense),
        new("Merchant Fees", AccountType.Expense),
        new("Taxes and Licenses", AccountType.Expense),
        new("Meals", AccountType.Expense),
        new("Travel", AccountType.Expense),
        new("Equipment", AccountType.Expense)
    ];
}
