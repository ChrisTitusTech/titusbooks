namespace FinancialApp.Core.Accounting;

public sealed record JournalEntry
{
    public required Guid Id { get; init; }

    public required Guid OrganizationId { get; init; }

    public required DateOnly EntryDate { get; init; }

    public string? Memo { get; init; }

    public Guid? SourceImportedTransactionId { get; init; }

    public bool IsVoid { get; init; }

    public DateTimeOffset? VoidedAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public required IReadOnlyList<JournalLine> Lines { get; init; }

    public decimal TotalDebits => Lines.Sum(line => line.Debit);

    public decimal TotalCredits => Lines.Sum(line => line.Credit);

    public bool IsBalanced => TotalDebits == TotalCredits && TotalDebits > 0;

    public void EnsureBalanced()
    {
        if (Lines.Count < 2)
        {
            throw new AccountingException("A posted journal entry must contain at least two lines.");
        }

        foreach (var line in Lines)
        {
            line.EnsureValid();
        }

        if (!IsBalanced)
        {
            throw new AccountingException("Journal entry is unbalanced. Total debits must equal total credits.");
        }
    }
}
