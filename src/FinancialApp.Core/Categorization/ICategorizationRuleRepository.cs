namespace FinancialApp.Core.Categorization;

public interface ICategorizationRuleRepository
{
    Task AddAsync(
        CategorizationRule rule,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategorizationRule>> ListAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategorizationRule>> ListActiveAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);
}
