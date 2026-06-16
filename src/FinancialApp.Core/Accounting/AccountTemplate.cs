namespace FinancialApp.Core.Accounting;

public sealed record AccountTemplate(
    string Name,
    AccountType AccountType,
    string? AccountSubtype = null,
    string Currency = "USD");
