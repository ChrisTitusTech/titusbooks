namespace FinancialApp.Api.Accounting;

public sealed record PostTransferRequest(
    DateOnly EntryDate,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string? Memo = null);
