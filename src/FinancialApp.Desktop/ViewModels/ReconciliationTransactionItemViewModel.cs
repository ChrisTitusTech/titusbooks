using CommunityToolkit.Mvvm.ComponentModel;
using FinancialApp.Core.Api;

namespace FinancialApp.Desktop.ViewModels;

public sealed partial class ReconciliationTransactionItemViewModel : ObservableObject
{
    public ReconciliationTransactionItemViewModel(ReconciliationTransactionSummary transaction)
    {
        JournalLineId = transaction.JournalLineId;
        EntryDate = transaction.EntryDate;
        Memo = transaction.Memo;
        Debit = transaction.Debit;
        Credit = transaction.Credit;
        OtherAccounts = transaction.OtherAccounts;
        IsReconciled = transaction.IsReconciled;
        isCleared = transaction.IsReconciled;
    }

    public Guid JournalLineId { get; }

    public DateOnly EntryDate { get; }

    public string? Memo { get; }

    public decimal Debit { get; }

    public decimal Credit { get; }

    public string OtherAccounts { get; }

    public bool IsReconciled { get; }

    public bool CanChange => !IsReconciled;

    [ObservableProperty]
    private bool isCleared;
}
