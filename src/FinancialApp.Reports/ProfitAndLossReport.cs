namespace FinancialApp.Reports;

public sealed record ProfitAndLossReport(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<AccountReportTotal> Income,
    IReadOnlyList<AccountReportTotal> Expenses)
{
    public decimal TotalIncome => Income.Sum(account => account.Amount);

    public decimal TotalExpenses => Expenses.Sum(account => account.Amount);

    public decimal NetIncome => TotalIncome - TotalExpenses;
}
