using FinancialApp.Core.Reconciliation;

namespace FinancialApp.Api.Reconciliation;

public sealed record ReconciliationPreviewResponse(
    Guid AccountId,
    DateOnly StatementEndDate,
    decimal StatementEndBalance,
    decimal ClearedBalance,
    decimal Difference,
    IReadOnlyList<ReconciliationTransactionResponse> Transactions)
{
    public static ReconciliationPreviewResponse FromPreview(ReconciliationPreview preview)
    {
        return new ReconciliationPreviewResponse(
            preview.AccountId,
            preview.StatementEndDate,
            preview.StatementEndBalance,
            preview.ClearedBalance,
            preview.Difference,
            preview.Transactions
                .Select(ReconciliationTransactionResponse.FromTransaction)
                .ToList());
    }
}
