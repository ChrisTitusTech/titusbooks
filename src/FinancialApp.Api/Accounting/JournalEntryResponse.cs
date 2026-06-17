using FinancialApp.Core.Accounting;

namespace FinancialApp.Api.Accounting;

public sealed record JournalEntryResponse(
    Guid Id,
    Guid OrganizationId,
    DateOnly EntryDate,
    string? Memo,
    decimal TotalDebits,
    decimal TotalCredits,
    IReadOnlyList<JournalLineResponse> Lines)
{
    public static JournalEntryResponse FromJournalEntry(JournalEntry entry)
    {
        return new JournalEntryResponse(
            entry.Id,
            entry.OrganizationId,
            entry.EntryDate,
            entry.Memo,
            entry.TotalDebits,
            entry.TotalCredits,
            entry.Lines.Select(JournalLineResponse.FromJournalLine).ToList());
    }
}
