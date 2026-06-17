using FinancialApp.Api.Accounting;
using FinancialApp.Api.Health;
using FinancialApp.Api.Organizations;
using FinancialApp.Api.Startup;
using FinancialApp.Core.Accounting;
using FinancialApp.Core.Organizations;
using FinancialApp.Data.Database;
using FinancialApp.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<DatabaseHealthCheck>();
builder.Services.AddSingleton<DatabaseMigrationStartupTask>();

var postgresConnectionString = ConnectionStringProvider.GetPostgresConnectionString(builder.Configuration);
if (!string.IsNullOrWhiteSpace(postgresConnectionString))
{
    builder.Services.AddSingleton(new DatabaseConnectionFactory(postgresConnectionString));
    builder.Services.AddScoped<IOrganizationRepository, PostgresOrganizationRepository>();
    builder.Services.AddScoped<IAccountRepository, PostgresAccountRepository>();
    builder.Services.AddScoped<IJournalEntryRepository, PostgresJournalEntryRepository>();
    builder.Services.AddScoped<DefaultChartOfAccountsSeeder>();
    builder.Services.AddScoped<AccountingService>();
}

var app = builder.Build();

app.Services
    .GetRequiredService<DatabaseMigrationStartupTask>()
    .RunIfEnabled();

app.MapGet("/", () => Results.Redirect("/health"));

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("healthy", "TitusBooks API", DateTimeOffset.UtcNow)));

app.MapGet("/health/database", async (
    [FromServices] DatabaseHealthCheck healthCheck,
    CancellationToken cancellationToken) =>
{
    var result = await healthCheck.CheckAsync(cancellationToken);
    return result.Status == "healthy"
        ? Results.Ok(result)
        : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/organizations", async (
    [FromServices] IOrganizationRepository organizationRepository,
    CancellationToken cancellationToken) =>
{
    var organizations = await organizationRepository.ListAsync(cancellationToken);
    return Results.Ok(organizations.Select(OrganizationResponse.FromOrganization));
});

app.MapPost("/organizations", async (
    CreateOrganizationRequest request,
    [FromServices] IOrganizationRepository organizationRepository,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Organization name is required." });
    }

    if (request.FiscalYearStartMonth is < 1 or > 12)
    {
        return Results.BadRequest(new { error = "Fiscal year start month must be between 1 and 12." });
    }

    var organization = new Organization
    {
        Id = Guid.NewGuid(),
        Name = request.Name.Trim(),
        BaseCurrency = string.IsNullOrWhiteSpace(request.BaseCurrency)
            ? "USD"
            : request.BaseCurrency.Trim().ToUpperInvariant(),
        FiscalYearStartMonth = request.FiscalYearStartMonth
    };

    await organizationRepository.AddAsync(organization, cancellationToken);

    return Results.Created($"/organizations/{organization.Id}", OrganizationResponse.FromOrganization(organization));
});

app.MapGet("/organizations/{organizationId:guid}/accounts", async (
    Guid organizationId,
    [FromServices] IOrganizationRepository organizationRepository,
    [FromServices] IAccountRepository accountRepository,
    CancellationToken cancellationToken) =>
{
    if (!await OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
    {
        return Results.NotFound();
    }

    var accounts = await accountRepository.ListByOrganizationAsync(organizationId, cancellationToken);
    return Results.Ok(accounts.Select(AccountResponse.FromAccount));
});

app.MapPost("/organizations/{organizationId:guid}/accounts", async (
    Guid organizationId,
    CreateAccountRequest request,
    [FromServices] IOrganizationRepository organizationRepository,
    [FromServices] IAccountRepository accountRepository,
    CancellationToken cancellationToken) =>
{
    if (!await OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
    {
        return Results.NotFound();
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Account name is required." });
    }

    if (!Enum.TryParse<AccountType>(request.AccountType, ignoreCase: true, out var accountType))
    {
        return Results.BadRequest(new { error = "Account type must be Asset, Liability, Equity, Income, or Expense." });
    }

    var existingAccount = await accountRepository.FindByNameAsync(organizationId, request.Name.Trim(), cancellationToken);
    if (existingAccount is not null)
    {
        return Results.Conflict(new { error = "An account with that name already exists." });
    }

    var account = new Account
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        Name = request.Name.Trim(),
        AccountType = accountType,
        AccountSubtype = string.IsNullOrWhiteSpace(request.AccountSubtype) ? null : request.AccountSubtype.Trim(),
        Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim().ToUpperInvariant(),
        ParentAccountId = request.ParentAccountId
    };

    await accountRepository.AddAsync(account, cancellationToken);

    return Results.Created($"/organizations/{organizationId}/accounts/{account.Id}", AccountResponse.FromAccount(account));
});

app.MapPut("/organizations/{organizationId:guid}/accounts/{accountId:guid}", async (
    Guid organizationId,
    Guid accountId,
    UpdateAccountRequest request,
    [FromServices] IOrganizationRepository organizationRepository,
    [FromServices] IAccountRepository accountRepository,
    CancellationToken cancellationToken) =>
{
    if (!await OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
    {
        return Results.NotFound();
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Account name is required." });
    }

    var accountName = request.Name.Trim();
    var account = await accountRepository.GetByIdAsync(organizationId, accountId, cancellationToken);
    if (account is null)
    {
        return Results.NotFound();
    }

    var existingAccount = await accountRepository.FindByNameAsync(organizationId, accountName, cancellationToken);
    if (existingAccount is not null && existingAccount.Id != accountId)
    {
        return Results.Conflict(new { error = "An account with that name already exists." });
    }

    var updatedAccount = account with
    {
        Name = accountName,
        AccountSubtype = string.IsNullOrWhiteSpace(request.AccountSubtype) ? null : request.AccountSubtype.Trim(),
        UpdatedAt = DateTimeOffset.UtcNow
    };

    await accountRepository.UpdateAsync(updatedAccount, cancellationToken);

    return Results.Ok(AccountResponse.FromAccount(updatedAccount));
});

app.MapPost("/organizations/{organizationId:guid}/accounts/{accountId:guid}/deactivate", async (
    Guid organizationId,
    Guid accountId,
    [FromServices] IOrganizationRepository organizationRepository,
    [FromServices] IAccountRepository accountRepository,
    CancellationToken cancellationToken) =>
{
    if (!await OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
    {
        return Results.NotFound();
    }

    var account = await accountRepository.GetByIdAsync(organizationId, accountId, cancellationToken);
    if (account is null)
    {
        return Results.NotFound();
    }

    await accountRepository.DeactivateAsync(organizationId, accountId, cancellationToken);

    return Results.Ok(AccountResponse.FromAccount(account with { IsActive = false, UpdatedAt = DateTimeOffset.UtcNow }));
});

app.MapPost("/organizations/{organizationId:guid}/accounts/defaults", async (
    Guid organizationId,
    [FromServices] IOrganizationRepository organizationRepository,
    [FromServices] DefaultChartOfAccountsSeeder seeder,
    CancellationToken cancellationToken) =>
{
    if (!await OrganizationExistsAsync(organizationRepository, organizationId, cancellationToken))
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

app.Run();

static async Task<bool> OrganizationExistsAsync(
    IOrganizationRepository organizationRepository,
    Guid organizationId,
    CancellationToken cancellationToken)
{
    return await organizationRepository.GetByIdAsync(organizationId, cancellationToken) is not null;
}

public partial class Program;
