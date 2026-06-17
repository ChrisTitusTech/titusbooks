using FinancialApp.Core.Organizations;

namespace FinancialApp.Api.Endpoints;

internal static class EndpointGuards
{
    public static async Task<bool> OrganizationExistsAsync(
        IOrganizationRepository organizationRepository,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return await organizationRepository.GetByIdAsync(organizationId, cancellationToken) is not null;
    }

    public static object Error(string message)
    {
        return new { error = message };
    }
}
