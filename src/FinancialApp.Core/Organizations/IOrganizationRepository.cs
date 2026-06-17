namespace FinancialApp.Core.Organizations;

public interface IOrganizationRepository
{
    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Organization>> ListAsync(CancellationToken cancellationToken = default);

    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
