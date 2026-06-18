namespace FinancialApp.Reports;

public sealed record AccountReportTotal(
    Guid AccountId,
    string AccountName,
    decimal Amount);
