namespace FinancialApp.Core.Api;

public sealed record JournalEntrySummary(
    Guid Id,
    Guid OrganizationId,
    DateOnly EntryDate,
    string? Memo,
    decimal TotalDebits,
    decimal TotalCredits,
    IReadOnlyList<JournalLineSummary> Lines);
