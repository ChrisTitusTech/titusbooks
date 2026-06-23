using FinancialApp.Api.Startup;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FinancialApp.Api.Tests;

public sealed class ConnectionStringProviderTests
{
    [Fact]
    public void GetPostgresConnectionString_AppliesSslConfigurationFromApiHostSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TITUSBOOKS_CONNECTIONSTRING"] =
                    "Host=db.example.test;Database=titusbooks;Username=api;Password=fake-secret",
                ["TITUSBOOKS_POSTGRES_SSL_MODE"] = "VerifyFull",
                ["TITUSBOOKS_POSTGRES_ROOT_CERTIFICATE"] = "/etc/titusbooks/postgres-root.crt",
            })
            .Build();

        var result = ConnectionStringProvider.GetPostgresConnectionString(configuration);
        var parsed = new NpgsqlConnectionStringBuilder(result);

        Assert.Equal(SslMode.VerifyFull, parsed.SslMode);
        Assert.Equal("/etc/titusbooks/postgres-root.crt", parsed.RootCertificate);
    }

    [Fact]
    public void GetPostgresConnectionString_DoesNotReadLegacyDatabaseConfigSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] =
                    "Host=db.example.test;Database=titusbooks;Username=api;Password=fake-secret",
                ["ConnectionStrings:Default"] =
                    "Host=db.example.test;Database=titusbooks;Username=api;Password=fake-secret",
            })
            .Build();

        Assert.Null(ConnectionStringProvider.GetPostgresConnectionString(configuration));
    }
}
