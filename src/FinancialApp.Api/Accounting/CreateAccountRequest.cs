namespace FinancialApp.Api.Accounting;

public sealed record CreateAccountRequest(
    string Name,
    string AccountType,
    string? AccountSubtype = null,
    string Currency = "USD",
    Guid? ParentAccountId = null);
