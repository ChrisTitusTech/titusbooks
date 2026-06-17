namespace FinancialApp.Core.Api;

public sealed record PostExpenseCommand(
    DateOnly EntryDate,
    Guid PaymentAccountId,
    Guid ExpenseAccountId,
    decimal Amount,
    string? Memo = null);
