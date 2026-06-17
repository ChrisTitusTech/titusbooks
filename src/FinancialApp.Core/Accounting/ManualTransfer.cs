namespace FinancialApp.Core.Accounting;

public sealed record ManualTransfer(
    Guid OrganizationId,
    DateOnly EntryDate,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string? Memo = null);
