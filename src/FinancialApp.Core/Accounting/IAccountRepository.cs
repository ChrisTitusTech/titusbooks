namespace FinancialApp.Core.Accounting;

public interface IAccountRepository
{
    Task<Account?> FindByNameAsync(Guid organizationId, string name, CancellationToken cancellationToken = default);

    Task<Account?> GetByIdAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken = default);

    Task AddAsync(Account account, CancellationToken cancellationToken = default);

    Task UpdateAsync(Account account, CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
