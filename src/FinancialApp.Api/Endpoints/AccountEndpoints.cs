using FinancialApp.Api.Accounting;
using FinancialApp.Core.Accounting;
using FinancialApp.Core.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace FinancialApp.Api.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/organizations/{organizationId:guid}/accounts", async (
            Guid organizationId,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] IAccountRepository accountRepository,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            var accounts = await accountRepository.ListByOrganizationAsync(organizationId, cancellationToken);
            return Results.Ok(accounts.Select(AccountResponse.FromAccount));
        });

        endpoints.MapPost("/organizations/{organizationId:guid}/accounts", async (
            Guid organizationId,
            CreateAccountRequest request,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] IAccountRepository accountRepository,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            if (!TryValidateAccountRequest(request, out var accountType, out var validationError))
            {
                return Results.BadRequest(EndpointGuards.Error(validationError));
            }

            var accountName = request.Name.Trim();
            var existingAccount = await accountRepository.FindByNameAsync(organizationId, accountName, cancellationToken);
            if (existingAccount is not null)
            {
                return Results.Conflict(EndpointGuards.Error("An account with that name already exists."));
            }

            var account = new Account
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = accountName,
                AccountType = accountType,
                AccountSubtype = NormalizeOptionalText(request.AccountSubtype),
                Currency = NormalizeCurrency(request.Currency),
                ParentAccountId = request.ParentAccountId
            };

            await accountRepository.AddAsync(account, cancellationToken);

            return Results.Created(
                $"/organizations/{organizationId}/accounts/{account.Id}",
                AccountResponse.FromAccount(account));
        });

        endpoints.MapPut("/organizations/{organizationId:guid}/accounts/{accountId:guid}", async (
            Guid organizationId,
            Guid accountId,
            UpdateAccountRequest request,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] IAccountRepository accountRepository,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(EndpointGuards.Error("Account name is required."));
            }

            var account = await accountRepository.GetByIdAsync(organizationId, accountId, cancellationToken);
            if (account is null)
            {
                return Results.NotFound();
            }

            var accountName = request.Name.Trim();
            var existingAccount = await accountRepository.FindByNameAsync(organizationId, accountName, cancellationToken);
            if (existingAccount is not null && existingAccount.Id != accountId)
            {
                return Results.Conflict(EndpointGuards.Error("An account with that name already exists."));
            }

            var updatedAccount = account with
            {
                Name = accountName,
                AccountSubtype = NormalizeOptionalText(request.AccountSubtype),
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await accountRepository.UpdateAsync(updatedAccount, cancellationToken);

            return Results.Ok(AccountResponse.FromAccount(updatedAccount));
        });

        endpoints.MapPost("/organizations/{organizationId:guid}/accounts/{accountId:guid}/deactivate", async (
            Guid organizationId,
            Guid accountId,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] IAccountRepository accountRepository,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            var account = await accountRepository.GetByIdAsync(organizationId, accountId, cancellationToken);
            if (account is null)
            {
                return Results.NotFound();
            }

            await accountRepository.DeactivateAsync(organizationId, accountId, cancellationToken);

            return Results.Ok(AccountResponse.FromAccount(account with
            {
                IsActive = false,
                UpdatedAt = DateTimeOffset.UtcNow
            }));
        });

        endpoints.MapPost("/organizations/{organizationId:guid}/accounts/defaults", async (
            Guid organizationId,
            [FromServices] IOrganizationRepository organizationRepository,
            [FromServices] DefaultChartOfAccountsSeeder seeder,
            CancellationToken cancellationToken) =>
        {
            if (!await EndpointGuards.OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
            {
                return Results.NotFound();
            }

            var createdAccounts = await seeder.SeedAsync(organizationId, cancellationToken: cancellationToken);
            var response = new SeedAccountsResponse(
                organizationId,
                createdAccounts.Count,
                createdAccounts.Select(AccountResponse.FromAccount).ToList());

            return Results.Ok(response);
        });

        return endpoints;
    }

    private static bool TryValidateAccountRequest(
        CreateAccountRequest request,
        out AccountType accountType,
        out string validationError)
    {
        accountType = default;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            validationError = "Account name is required.";
            return false;
        }

        if (!Enum.TryParse(request.AccountType, ignoreCase: true, out accountType))
        {
            validationError = "Account type must be Asset, Liability, Equity, Income, or Expense.";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private static string NormalizeCurrency(string currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "USD"
            : currency.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
