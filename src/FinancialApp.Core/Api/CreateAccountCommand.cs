namespace FinancialApp.Core.Api;

public sealed record CreateAccountCommand(
    string Name,
    string AccountType,
    string? AccountSubtype = null,
    string Currency = "USD",
    Guid? ParentAccountId = null);
