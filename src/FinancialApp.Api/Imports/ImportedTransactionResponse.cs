using FinancialApp.Core.Imports;

namespace FinancialApp.Api.Imports;

public sealed record ImportedTransactionResponse(
    Guid Id,
    Guid? ImportBatchId,
    string Source,
    DateOnly PostedDate,
    string Description,
    decimal Amount,
    string Currency,
    string Status)
{
    public static ImportedTransactionResponse FromTransaction(ImportedTransaction transaction)
    {
        return new ImportedTransactionResponse(
            transaction.Id,
            transaction.ImportBatchId,
            transaction.Source,
            transaction.PostedDate,
            transaction.Description,
            transaction.Amount,
            transaction.Currency,
            transaction.Status.ToString().ToLowerInvariant());
    }
}
