using FinancialApp.Core.Reconciliation;

namespace FinancialApp.Api.Reconciliation;

public sealed record ReconciliationTransactionResponse(
    Guid JournalLineId,
    Guid JournalEntryId,
    DateOnly EntryDate,
    string? Memo,
    decimal Debit,
    decimal Credit,
    string OtherAccounts,
    bool IsReconciled)
{
    public static ReconciliationTransactionResponse FromTransaction(
        ReconciliationTransaction transaction)
    {
        return new ReconciliationTransactionResponse(
            transaction.JournalLineId,
            transaction.JournalEntryId,
            transaction.EntryDate,
            transaction.Memo,
            transaction.Debit,
            transaction.Credit,
            transaction.OtherAccounts,
            transaction.IsReconciled);
    }
}
