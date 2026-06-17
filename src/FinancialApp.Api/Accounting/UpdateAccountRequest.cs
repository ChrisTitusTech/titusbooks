namespace FinancialApp.Api.Accounting;

public sealed record UpdateAccountRequest(string Name, string? AccountSubtype = null);
