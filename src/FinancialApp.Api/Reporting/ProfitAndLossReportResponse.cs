using FinancialApp.Reports;

namespace FinancialApp.Api.Reporting;

public sealed record ProfitAndLossReportResponse(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<AccountReportTotalResponse> Income,
    IReadOnlyList<AccountReportTotalResponse> Expenses,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetIncome)
{
    public static ProfitAndLossReportResponse FromReport(ProfitAndLossReport report)
    {
        return new ProfitAndLossReportResponse(
            report.StartDate,
            report.EndDate,
            report.Income.Select(AccountReportTotalResponse.FromReportTotal).ToList(),
            report.Expenses.Select(AccountReportTotalResponse.FromReportTotal).ToList(),
            report.TotalIncome,
            report.TotalExpenses,
            report.NetIncome);
    }
}
