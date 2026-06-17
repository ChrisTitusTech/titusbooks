namespace FinancialApp.Core.Accounting;

public sealed record ManualExpense(
    Guid OrganizationId,
    DateOnly EntryDate,
    Guid PaymentAccountId,
    Guid ExpenseAccountId,
    decimal Amount,
    string? Memo = null);
