using FinancialApp.Api.Imports;
using FinancialApp.Core.Accounting;
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
            string? status,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] IImportRepository importRepository,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            ImportedTransactionStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<ImportedTransactionStatus>(
                        status,
                        ignoreCase: true,
                        out var parsedStatus))
                {
                    return Results.BadRequest(EndpointGuards.Error(
                        "Import status filter is not supported."));
                }

                statusFilter = parsedStatus;
            }

            var transactions = await importRepository.ListTransactionsAsync(
                organizationId,
                statusFilter,
                cancellationToken);
            return Results.Ok(transactions.Select(ImportedTransactionResponse.FromTransaction));
        });

        endpoints.MapPost("/organizations/{organizationId:guid}/imports/transactions/categorize", async (
            Guid organizationId,
            CategorizeImportedTransactionsRequest request,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] IAccountRepository accountRepository,
            [FromServices] IImportRepository importRepository,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(
                    organizationRepository,
                    organizationId,
                    cancellationToken))
            {
                return Results.NotFound();
            }

            if (request.TransactionIds.Count == 0)
            {
                return Results.BadRequest(EndpointGuards.Error(
                    "Select at least one imported transaction."));
            }

            var account = await accountRepository.GetByIdAsync(
                organizationId,
                request.CategoryAccountId,
                cancellationToken);
            if (account is null || !account.IsActive)
            {
                return Results.BadRequest(EndpointGuards.Error(
                    "Category must be an active account in this organization."));
            }

            var categorized = await importRepository.CategorizeTransactionsAsync(
                organizationId,
                request.TransactionIds,
                request.CategoryAccountId,
                cancellationToken);
            if (!categorized)
            {
                return Results.BadRequest(EndpointGuards.Error(
                    "Every selected transaction must exist and be pending or categorized."));
            }

            return Results.Ok(new
            {
                CategorizedCount = request.TransactionIds.Distinct().Count()
            });
        });

        endpoints.MapPost("/organizations/{organizationId:guid}/imports/transactions/post", async (
            Guid organizationId,
            PostImportedTransactionsRequest request,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] ImportPostingService postingService,
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
                var result = await postingService.PostAsync(
                    organizationId,
                    request.SourceAccountId,
                    request.TransactionIds ?? [],
                    request.MerchantFeeAccountId,
                    cancellationToken);
                return Results.Ok(new PostImportedTransactionsResponse(
                    result.PostedCount,
                    result.JournalEntryIds));
            }
            catch (ImportPostingException exception)
            {
                return Results.BadRequest(EndpointGuards.Error(exception.Message));
            }
        });

        return endpoints;
    }
}
