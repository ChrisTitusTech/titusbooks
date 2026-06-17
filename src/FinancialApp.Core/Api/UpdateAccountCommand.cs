namespace FinancialApp.Core.Api;

public sealed record UpdateAccountCommand(string Name, string? AccountSubtype = null);
