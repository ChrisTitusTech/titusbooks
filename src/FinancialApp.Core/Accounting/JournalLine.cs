namespace FinancialApp.Core.Accounting;

public sealed record JournalLine
{
    public required Guid Id { get; init; }

    public required Guid JournalEntryId { get; init; }

    public required Guid AccountId { get; init; }

    public decimal Debit { get; init; }

    public decimal Credit { get; init; }

    public string? Memo { get; init; }

    public void EnsureValid()
    {
        if (Debit < 0 || Credit < 0)
        {
            throw new AccountingException("Journal line debit and credit amounts cannot be negative.");
        }

        if (Debit > 0 && Credit > 0)
        {
            throw new AccountingException("A journal line cannot contain both a debit and a credit amount.");
        }

        if (Debit == 0 && Credit == 0)
        {
            throw new AccountingException("A journal line must contain either a debit or a credit amount.");
        }
    }
}
