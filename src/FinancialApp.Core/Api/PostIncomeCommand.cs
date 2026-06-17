namespace FinancialApp.Core.Api;

public sealed record PostIncomeCommand(
    DateOnly EntryDate,
    Guid DepositAccountId,
    Guid IncomeAccountId,
    decimal Amount,
    string? Memo = null);
