using Npgsql;

namespace FinancialApp.Api.Startup;

public static class ConnectionStringProvider
{
    public static string? GetPostgresConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration["TITUSBOOKS_CONNECTIONSTRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var sslMode = configuration["TITUSBOOKS_POSTGRES_SSL_MODE"];
        var rootCertificate = configuration["TITUSBOOKS_POSTGRES_ROOT_CERTIFICATE"];

        if (!string.IsNullOrWhiteSpace(sslMode))
        {
            if (!Enum.TryParse<SslMode>(sslMode, ignoreCase: true, out var parsedSslMode))
            {
                throw new InvalidOperationException(
                    "TITUSBOOKS_POSTGRES_SSL_MODE must be Disable, Allow, Prefer, Require, VerifyCA, or VerifyFull.");
            }

            builder.SslMode = parsedSslMode;
        }

        if (!string.IsNullOrWhiteSpace(rootCertificate))
        {
            builder.RootCertificate = rootCertificate;
        }

        return builder.ConnectionString;
    }
}
