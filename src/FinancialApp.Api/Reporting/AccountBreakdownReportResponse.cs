using FinancialApp.Reports;

namespace FinancialApp.Api.Reporting;

public sealed record AccountBreakdownReportResponse(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<AccountReportTotalResponse> Accounts,
    decimal Total)
{
    public static AccountBreakdownReportResponse FromReport(AccountBreakdownReport report)
    {
        return new AccountBreakdownReportResponse(
            report.StartDate,
            report.EndDate,
            report.Accounts.Select(AccountReportTotalResponse.FromReportTotal).ToList(),
            report.Total);
    }
}
