namespace FinancialApp.Core.Accounting;

public interface IAccountRepository
{
    Task<Account?> FindByNameAsync(Guid organizationId, string name, CancellationToken cancellationToken = default);

    Task AddAsync(Account account, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
