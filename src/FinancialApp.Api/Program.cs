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
    builder.Services.AddScoped<DefaultChartOfAccountsSeeder>();
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
    [FromServices] IAccountRepository accountRepository,
    CancellationToken cancellationToken) =>
{
    var accounts = await accountRepository.ListByOrganizationAsync(organizationId, cancellationToken);
    return Results.Ok(accounts.Select(AccountResponse.FromAccount));
});

app.MapPost("/organizations/{organizationId:guid}/accounts/defaults", async (
    Guid organizationId,
    [FromServices] DefaultChartOfAccountsSeeder seeder,
    CancellationToken cancellationToken) =>
{
    var createdAccounts = await seeder.SeedAsync(organizationId, cancellationToken: cancellationToken);
    var response = new SeedAccountsResponse(
        organizationId,
        createdAccounts.Count,
        createdAccounts.Select(AccountResponse.FromAccount).ToList());

    return Results.Ok(response);
});

app.Run();

public partial class Program;
