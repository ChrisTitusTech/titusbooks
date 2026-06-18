using FinancialApp.Reports;

namespace FinancialApp.Api.Reporting;

public sealed record AccountReportTotalResponse(
    Guid AccountId,
    string AccountName,
    decimal Amount)
{
    public static AccountReportTotalResponse FromReportTotal(AccountReportTotal total)
    {
        return new AccountReportTotalResponse(total.AccountId, total.AccountName, total.Amount);
    }
}
