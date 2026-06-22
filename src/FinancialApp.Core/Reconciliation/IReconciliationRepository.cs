namespace FinancialApp.Core.Reconciliation;

public interface IReconciliationRepository
{
    Task<IReadOnlyList<ReconciliationTransaction>> ListTransactionsAsync(
        Guid organizationId,
        Guid accountId,
        DateOnly statementEndDate,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Reconciliation reconciliation,
        IReadOnlyCollection<Guid> journalLineIds,
        CancellationToken cancellationToken = default);
}
