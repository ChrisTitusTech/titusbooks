namespace FinancialApp.Core.Accounting;

public sealed record ManualIncome(
    Guid OrganizationId,
    DateOnly EntryDate,
    Guid DepositAccountId,
    Guid IncomeAccountId,
    decimal Amount,
    string? Memo = null);
