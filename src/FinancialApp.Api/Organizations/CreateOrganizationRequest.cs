namespace FinancialApp.Api.Organizations;

public sealed record CreateOrganizationRequest(
    string Name,
    string BaseCurrency = "USD",
    int FiscalYearStartMonth = 1);
