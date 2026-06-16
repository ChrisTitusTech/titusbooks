namespace FinancialApp.Core.Application;

public sealed record AppSettings
{
    public string ApplicationName { get; init; } = "TitusBooks";

    public string EnvironmentName { get; init; } = "Development";

    public DatabaseSettings Database { get; init; } = new();
}
