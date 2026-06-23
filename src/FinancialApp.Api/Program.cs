using FinancialApp.Api.Endpoints;
using FinancialApp.Api.Errors;
using FinancialApp.Api.Health;
using FinancialApp.Api.Startup;
using FinancialApp.Core.Accounting;
using FinancialApp.Core.Categorization;
using FinancialApp.Core.Imports;
using FinancialApp.Core.Organizations;
using FinancialApp.Core.Reconciliation;
using FinancialApp.Data.Database;
using FinancialApp.Data.Repositories;
using FinancialApp.Importers;
using FinancialApp.Reports;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<DatabaseHealthCheck>();
builder.Services.AddSingleton<DatabaseMigrationStartupTask>();

var postgresConnectionString = ConnectionStringProvider.GetPostgresConnectionString(builder.Configuration);
if (!string.IsNullOrWhiteSpace(postgresConnectionString))
{
    builder.Services.AddSingleton(new DatabaseConnectionFactory(postgresConnectionString));
    builder.Services.AddScoped<IOrganizationRepository, PostgresOrganizationRepository>();
    builder.Services.AddScoped<IAccountRepository, PostgresAccountRepository>();
    builder.Services.AddScoped<IJournalEntryRepository, PostgresJournalEntryRepository>();
    builder.Services.AddScoped<IImportRepository, PostgresImportRepository>();
    builder.Services.AddScoped<IImportPostingRepository, PostgresImportPostingRepository>();
    builder.Services.AddScoped<ICategorizationRuleRepository, PostgresCategorizationRuleRepository>();
    builder.Services.AddScoped<IFinancialReportRepository, PostgresFinancialReportRepository>();
    builder.Services.AddScoped<IReconciliationRepository, PostgresReconciliationRepository>();
    builder.Services.AddScoped<DefaultChartOfAccountsSeeder>();
    builder.Services.AddScoped<AccountingService>();
    builder.Services.AddScoped<FinancialReportService>();
    builder.Services.AddScoped<ReconciliationService>();
    builder.Services.AddScoped<GenericCsvParser>();
    builder.Services.AddScoped<PayPalCsvParser>();
    builder.Services.AddScoped<CsvImportService>();
    builder.Services.AddScoped<ImportPostingService>();
    builder.Services.AddSingleton<CategorizationRuleEngine>();
}

var app = builder.Build();

app.UseExceptionHandler();

app.Services
    .GetRequiredService<DatabaseMigrationStartupTask>()
    .RunIfEnabled();

app.MapHealthEndpoints();
app.MapOrganizationEndpoints();
app.MapAccountEndpoints();
app.MapTransactionEndpoints();
app.MapReportEndpoints();
app.MapImportEndpoints();
app.MapCategorizationRuleEndpoints();
app.MapReconciliationEndpoints();

app.Run();

public partial class Program;
