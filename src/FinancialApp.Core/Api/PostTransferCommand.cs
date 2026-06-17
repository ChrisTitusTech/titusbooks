namespace FinancialApp.Core.Api;

public sealed record PostTransferCommand(
    DateOnly EntryDate,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string? Memo = null);
