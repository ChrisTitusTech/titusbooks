using FinancialApp.Api.Reconciliation;
using FinancialApp.Core.Organizations;
using FinancialApp.Core.Reconciliation;
using Microsoft.AspNetCore.Mvc;

namespace FinancialApp.Api.Endpoints;

public static class ReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapReconciliationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/organizations/{organizationId:guid}/accounts/{accountId:guid}/reconciliation/preview",
            async (
                Guid organizationId,
                Guid accountId,
                PreviewReconciliationRequest request,
                [FromServices] IOrganizationRepository organizationRepository,
                [FromServices] ReconciliationService reconciliationService,
                CancellationToken cancellationToken) =>
            {
                if (!await EndpointGuards.OrganizationExistsAsync(
                    organizationRepository,
                    organizationId,
                    cancellationToken))
                {
                    return Results.NotFound();
                }

                try
                {
                    var preview = await reconciliationService.PreviewAsync(
                        organizationId,
                        accountId,
                        request.StatementEndDate,
                        request.StatementEndBalance,
                        request.ClearedJournalLineIds ?? [],
                        cancellationToken);
                    return Results.Ok(ReconciliationPreviewResponse.FromPreview(preview));
                }
                catch (ReconciliationException exception)
                {
                    return Results.BadRequest(EndpointGuards.Error(exception.Message));
                }
            });

        endpoints.MapPost(
            "/organizations/{organizationId:guid}/accounts/{accountId:guid}/reconciliation/complete",
            async (
                Guid organizationId,
                Guid accountId,
                CompleteReconciliationRequest request,
                [FromServices] IOrganizationRepository organizationRepository,
                [FromServices] ReconciliationService reconciliationService,
                CancellationToken cancellationToken) =>
            {
                if (!await EndpointGuards.OrganizationExistsAsync(
                    organizationRepository,
                    organizationId,
                    cancellationToken))
                {
                    return Results.NotFound();
                }

                try
                {
                    var completed = await reconciliationService.CompleteAsync(
                        organizationId,
                        accountId,
                        request.StatementEndDate,
                        request.StatementEndBalance,
                        request.ClearedJournalLineIds ?? [],
                        cancellationToken);
                    return Results.Ok(ReconciliationPreviewResponse.FromPreview(completed));
                }
                catch (ReconciliationException exception)
                {
                    return Results.BadRequest(EndpointGuards.Error(exception.Message));
                }
            });

        return endpoints;
    }
}
