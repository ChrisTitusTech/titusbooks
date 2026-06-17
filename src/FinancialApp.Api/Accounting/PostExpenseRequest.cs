namespace FinancialApp.Api.Accounting;

public sealed record PostExpenseRequest(
    DateOnly EntryDate,
    Guid PaymentAccountId,
    Guid ExpenseAccountId,
    decimal Amount,
    string? Memo = null);
