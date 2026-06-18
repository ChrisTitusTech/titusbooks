using FinancialApp.Api.Imports;
using FinancialApp.Core.Imports;
using FinancialApp.Core.Organizations;
using FinancialApp.Importers;
using Microsoft.AspNetCore.Mvc;

namespace FinancialApp.Api.Endpoints;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/imports/csv/headers", (
            CsvHeadersRequest request,
            [FromServices] GenericCsvParser parser) =>
        {
            try
            {
                return Results.Ok(parser.ReadHeaders(request.CsvContent));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(EndpointGuards.Error(exception.Message));
            }
        });

        endpoints.MapPost("/organizations/{organizationId:guid}/imports/csv/preview", async (
            Guid organizationId,
            CsvImportApiRequest request,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] CsvImportService importService,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            try
            {
                return Results.Ok(CsvImportPreviewResponse.FromPreview(
                    importService.Preview(request.ToImportRequest(organizationId))));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(EndpointGuards.Error(exception.Message));
            }
        });

        endpoints.MapPost("/organizations/{organizationId:guid}/imports/csv", async (
            Guid organizationId,
            CsvImportApiRequest request,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] CsvImportService importService,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            try
            {
                var result = await importService.ImportAsync(
                    request.ToImportRequest(organizationId),
                    cancellationToken);
                return Results.Ok(CsvImportResultResponse.FromResult(result));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(EndpointGuards.Error(exception.Message));
            }
        });

        endpoints.MapGet("/organizations/{organizationId:guid}/imports/transactions", async (
            Guid organizationId,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] IImportRepository importRepository,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            var transactions = await importRepository.ListTransactionsAsync(
                organizationId,
                cancellationToken);
            return Results.Ok(transactions.Select(ImportedTransactionResponse.FromTransaction));
        });

        return endpoints;
    }
}
