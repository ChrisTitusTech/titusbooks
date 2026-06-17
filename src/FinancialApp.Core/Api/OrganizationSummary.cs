namespace FinancialApp.Core.Api;

public sealed record OrganizationSummary(
    Guid Id,
    string Name,
    string BaseCurrency,
    int FiscalYearStartMonth,
    string AccountingMethod);
