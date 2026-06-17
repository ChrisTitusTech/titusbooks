namespace FinancialApp.Api.Startup;

public static class ConnectionStringProvider
{
    public static string? GetPostgresConnectionString(IConfiguration configuration)
    {
        return configuration["TITUSBOOKS_CONNECTIONSTRING"]
            ?? configuration.GetConnectionString("Default")
            ?? configuration["Database:ConnectionString"];
    }
}
