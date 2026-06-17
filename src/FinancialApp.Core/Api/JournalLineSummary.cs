namespace FinancialApp.Core.Api;

public sealed record JournalLineSummary(
    Guid Id,
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string? Memo);
