using FinancialApp.Data.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("FinancialApp.Migrations");

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .AddEnvironmentVariables(prefix: "TITUSBOOKS_")
    .Build();

var connectionString = GetConnectionString(args, configuration, LoadDotEnvValue("TITUSBOOKS_CONNECTIONSTRING"));

if (string.IsNullOrWhiteSpace(connectionString))
{
    logger.LogError("No PostgreSQL connection string was provided.");
    Console.Error.WriteLine("Provide a connection string with --connection-string or TITUSBOOKS_CONNECTIONSTRING.");
    return 2;
}

var runner = new DatabaseMigrationRunner(connectionString);
var result = runner.Run();

if (!result.Succeeded)
{
    logger.LogError("Database migrations failed: {ErrorMessage}", result.ErrorMessage);
    return 1;
}

logger.LogInformation("Database migrations completed successfully.");
return 0;

static string? GetConnectionString(string[] args, IConfiguration configuration, string? dotEnvConnectionString)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] is "--connection-string" or "-c")
        {
            return i + 1 < args.Length ? args[i + 1] : null;
        }

        const string prefix = "--connection-string=";
        if (args[i].StartsWith(prefix, StringComparison.Ordinal))
        {
            return args[i][prefix.Length..];
        }
    }

    return configuration["ConnectionString"]
        ?? configuration.GetConnectionString("Default")
        ?? configuration["Database:ConnectionString"]
        ?? dotEnvConnectionString;
}

static string? LoadDotEnvValue(string key)
{
    var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");

    if (!File.Exists(envPath))
    {
        return null;
    }

    foreach (var rawLine in File.ReadLines(envPath))
    {
        var line = rawLine.Trim();

        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            continue;
        }

        var name = line[..separatorIndex].Trim();
        if (!string.Equals(name, key, StringComparison.Ordinal))
        {
            continue;
        }

        var value = line[(separatorIndex + 1)..].Trim().Trim('"');
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    return null;
}
