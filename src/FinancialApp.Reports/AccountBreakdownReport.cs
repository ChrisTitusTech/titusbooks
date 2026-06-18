namespace FinancialApp.Reports;

public sealed record AccountBreakdownReport(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<AccountReportTotal> Accounts)
{
    public decimal Total => Accounts.Sum(account => account.Amount);
}
