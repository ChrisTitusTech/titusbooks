namespace FinancialApp.Core.Reconciliation;

public sealed record ReconciliationTransaction(
    Guid JournalLineId,
    Guid JournalEntryId,
    DateOnly EntryDate,
    string? Memo,
    decimal Debit,
    decimal Credit,
    string OtherAccounts,
    Guid? ReconciliationId)
{
    public bool IsReconciled => ReconciliationId is not null;
}
