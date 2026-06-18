namespace FinancialApp.Core.Api;

public sealed record AccountReportTotalSummary(
    Guid AccountId,
    string AccountName,
    decimal Amount);
