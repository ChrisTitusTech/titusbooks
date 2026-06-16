using System.Reflection;
using DbUp;

namespace FinancialApp.Data.Migrations;

public sealed class DatabaseMigrationRunner
{
    private readonly string connectionString;

    public DatabaseMigrationRunner(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
    }

    public MigrationRunResult Run()
    {
        var upgradeEngine = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithExecutionTimeout(TimeSpan.FromMinutes(5))
            .WithTransaction()
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                scriptName => scriptName.Contains(".Migrations.", StringComparison.Ordinal))
            .LogToConsole()
            .Build();

        var result = upgradeEngine.PerformUpgrade();

        return result.Successful
            ? MigrationRunResult.Success()
            : MigrationRunResult.Failure(result.Error);
    }
}
