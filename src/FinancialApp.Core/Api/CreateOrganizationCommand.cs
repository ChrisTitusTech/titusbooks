namespace FinancialApp.Core.Api;

public sealed record CreateOrganizationCommand(
    string Name,
    string BaseCurrency = "USD",
    int FiscalYearStartMonth = 1);
