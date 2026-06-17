using FinancialApp.Api.Organizations;
using FinancialApp.Core.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace FinancialApp.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/organizations", async (
            [FromServices] IOrganizationRepository organizationRepository,
            CancellationToken cancellationToken) =>
        {
            var organizations = await organizationRepository.ListAsync(cancellationToken);
            return Results.Ok(organizations.Select(OrganizationResponse.FromOrganization));
        });

        endpoints.MapPost("/organizations", async (
            CreateOrganizationRequest request,
            [FromServices] IOrganizationRepository organizationRepository,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(EndpointGuards.Error("Organization name is required."));
            }

            if (request.FiscalYearStartMonth is < 1 or > 12)
            {
                return Results.BadRequest(EndpointGuards.Error("Fiscal year start month must be between 1 and 12."));
            }

            var organization = new Organization
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                BaseCurrency = NormalizeCurrency(request.BaseCurrency),
                FiscalYearStartMonth = request.FiscalYearStartMonth
            };

            await organizationRepository.AddAsync(organization, cancellationToken);

            return Results.Created(
                $"/organizations/{organization.Id}",
                OrganizationResponse.FromOrganization(organization));
        });

        return endpoints;
    }

    private static string NormalizeCurrency(string currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "USD"
            : currency.Trim().ToUpperInvariant();
    }
}
