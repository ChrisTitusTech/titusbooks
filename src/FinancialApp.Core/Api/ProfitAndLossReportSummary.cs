namespace FinancialApp.Core.Api;

public sealed record ProfitAndLossReportSummary(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<AccountReportTotalSummary> Income,
    IReadOnlyList<AccountReportTotalSummary> Expenses,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetIncome);
