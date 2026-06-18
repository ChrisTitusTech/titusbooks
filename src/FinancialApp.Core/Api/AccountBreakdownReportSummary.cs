namespace FinancialApp.Core.Api;

public sealed record AccountBreakdownReportSummary(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<AccountReportTotalSummary> Accounts,
    decimal Total);
