using CommunityToolkit.Mvvm.ComponentModel;
using FinancialApp.Core.Api;

namespace FinancialApp.Desktop.ViewModels;

public sealed partial class ImportInboxItemViewModel : ObservableObject
{
    public ImportInboxItemViewModel(ImportedTransactionSummary transaction)
    {
        Transaction = transaction;
    }

    [ObservableProperty]
    private bool isSelected;

    public ImportedTransactionSummary Transaction { get; }

    public Guid Id => Transaction.Id;

    public DateOnly PostedDate => Transaction.PostedDate;

    public string Description => Transaction.Description;

    public decimal Amount => Transaction.Amount;

    public string Status => Transaction.Status;

    public Guid? CategoryAccountId => Transaction.CategoryAccountId;
}
