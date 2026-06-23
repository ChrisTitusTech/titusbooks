using FinancialApp.Data.Migrations;

namespace FinancialApp.Api.Startup;

public sealed class DatabaseMigrationStartupTask
{
    private readonly IConfiguration configuration;
    private readonly ILogger<DatabaseMigrationStartupTask> logger;

    public DatabaseMigrationStartupTask(
        IConfiguration configuration,
        ILogger<DatabaseMigrationStartupTask> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
    }

    public void RunIfEnabled()
    {
        if (!configuration.GetValue("TITUSBOOKS_RUN_MIGRATIONS", false))
        {
            return;
        }

        var connectionString = ConnectionStringProvider.GetPostgresConnectionString(configuration);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database migrations are enabled, but no PostgreSQL connection string is configured.");
        }

        logger.LogInformation("Applying database migrations.");

        var result = new DatabaseMigrationRunner(connectionString).Run();

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Database migrations failed. Review the API host logs.");
        }

        logger.LogInformation("Database migrations completed.");
    }
}
