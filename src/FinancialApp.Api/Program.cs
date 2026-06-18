using FinancialApp.Api.Endpoints;
using FinancialApp.Api.Health;
using FinancialApp.Api.Startup;
using FinancialApp.Core.Accounting;
using FinancialApp.Core.Organizations;
using FinancialApp.Data.Database;
using FinancialApp.Data.Repositories;
using FinancialApp.Reports;

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
    builder.Services.AddScoped<IFinancialReportRepository, PostgresFinancialReportRepository>();
    builder.Services.AddScoped<DefaultChartOfAccountsSeeder>();
    builder.Services.AddScoped<AccountingService>();
    builder.Services.AddScoped<FinancialReportService>();
}

var app = builder.Build();

app.Services
    .GetRequiredService<DatabaseMigrationStartupTask>()
    .RunIfEnabled();

app.MapHealthEndpoints();
app.MapOrganizationEndpoints();
app.MapAccountEndpoints();
app.MapTransactionEndpoints();
app.MapReportEndpoints();

app.Run();

public partial class Program;
