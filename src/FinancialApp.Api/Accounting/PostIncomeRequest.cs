namespace FinancialApp.Api.Accounting;

public sealed record PostIncomeRequest(
    DateOnly EntryDate,
    Guid DepositAccountId,
    Guid IncomeAccountId,
    decimal Amount,
    string? Memo = null);
