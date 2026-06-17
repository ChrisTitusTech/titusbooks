using FinancialApp.Api.Accounting;
using FinancialApp.Core.Accounting;
using FinancialApp.Core.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace FinancialApp.Api.Endpoints;

public static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/organizations/{organizationId:guid}/transactions/expenses", async (
            Guid organizationId,
            PostExpenseRequest request,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] AccountingService accountingService,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            return await PostManualTransactionAsync(
                organizationId,
                accountingService.PostExpenseAsync(
                    new ManualExpense(
                        organizationId,
                        request.EntryDate,
                        request.PaymentAccountId,
                        request.ExpenseAccountId,
                        request.Amount,
                        request.Memo),
                    cancellationToken));
        });

        endpoints.MapPost("/organizations/{organizationId:guid}/transactions/income", async (
            Guid organizationId,
            PostIncomeRequest request,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] AccountingService accountingService,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            return await PostManualTransactionAsync(
                organizationId,
                accountingService.PostIncomeAsync(
                    new ManualIncome(
                        organizationId,
                        request.EntryDate,
                        request.DepositAccountId,
                        request.IncomeAccountId,
                        request.Amount,
                        request.Memo),
                    cancellationToken));
        });

        endpoints.MapPost("/organizations/{organizationId:guid}/transactions/transfers", async (
            Guid organizationId,
            PostTransferRequest request,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] AccountingService accountingService,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            return await PostManualTransactionAsync(
                organizationId,
                accountingService.PostTransferAsync(
                    new ManualTransfer(
                        organizationId,
                        request.EntryDate,
                        request.FromAccountId,
                        request.ToAccountId,
                        request.Amount,
                        request.Memo),
                    cancellationToken));
        });

        endpoints.MapGet("/organizations/{organizationId:guid}/accounts/{accountId:guid}/register", async (
            Guid organizationId,
            Guid accountId,
            DateOnly? startDate,
            DateOnly? endDate,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] IAccountRepository accountRepository,
            [FromServices] IJournalEntryRepository journalEntryRepository,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            if (await accountRepository.GetByIdAsync(organizationId, accountId, cancellationToken) is null)
            {
                return Results.NotFound();
            }

            var entries = await journalEntryRepository.ListRegisterAsync(
                organizationId,
                accountId,
                startDate,
                endDate,
                cancellationToken);

            return Results.Ok(entries.Select(AccountRegisterEntryResponse.FromRegisterEntry));
        });

        return endpoints;
    }

    private static async Task<IResult> PostManualTransactionAsync(
        Guid organizationId,
        Task<JournalEntry> postTransactionTask)
    {
        try
        {
            var entry = await postTransactionTask;
            return Results.Created(
                $"/organizations/{organizationId}/journal-entries/{entry.Id}",
                JournalEntryResponse.FromJournalEntry(entry));
        }
        catch (AccountingException exception)
        {
            return Results.BadRequest(EndpointGuards.Error(exception.Message));
        }
    }
}
