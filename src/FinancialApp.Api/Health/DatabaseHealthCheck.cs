using FinancialApp.Api.Startup;
using Npgsql;

namespace FinancialApp.Api.Health;

public sealed class DatabaseHealthCheck
{
    private readonly string? connectionString;
    private readonly ILogger<DatabaseHealthCheck> logger;

    public DatabaseHealthCheck(IConfiguration configuration, ILogger<DatabaseHealthCheck> logger)
    {
        connectionString = ConnectionStringProvider.GetPostgresConnectionString(configuration);
        this.logger = logger;
    }

    public async Task<DatabaseHealthResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return DatabaseHealthResult.Unhealthy("Database connection string is not configured.");
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand("select 1", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);

            return Equals(result, 1)
                ? DatabaseHealthResult.Healthy()
                : DatabaseHealthResult.Unhealthy("Database health query returned an unexpected result.");
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or OperationCanceledException)
        {
            logger.LogWarning(exception, "Database health check failed.");
            return DatabaseHealthResult.Unhealthy("Database connection failed.");
        }
    }
}
