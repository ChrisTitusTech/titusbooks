using FinancialApp.Core.Imports;

namespace FinancialApp.Api.Imports;

public sealed record ImportedTransactionResponse(
    Guid Id,
    Guid? ImportBatchId,
    string Source,
    string? SourceType,
    string? SourceStatus,
    string Kind,
    DateOnly PostedDate,
    string Description,
    decimal Amount,
    decimal? Balance,
    decimal? GrossAmount,
    decimal? FeeAmount,
    decimal? NetAmount,
    string Currency,
    string Status,
    Guid? CategoryAccountId,
    Guid? MatchedRuleId)
{
    public static ImportedTransactionResponse FromTransaction(ImportedTransaction transaction)
    {
        return new ImportedTransactionResponse(
            transaction.Id,
            transaction.ImportBatchId,
            transaction.Source,
            transaction.SourceType,
            transaction.SourceStatus,
            ImportedTransactionKindNames.ToStorageValue(transaction.Kind),
            transaction.PostedDate,
            transaction.Description,
            transaction.Amount,
            transaction.Balance,
            transaction.GrossAmount,
            transaction.FeeAmount,
            transaction.NetAmount,
            transaction.Currency,
            transaction.Status.ToString().ToLowerInvariant(),
            transaction.CategoryAccountId,
            transaction.MatchedRuleId);
    }
}
