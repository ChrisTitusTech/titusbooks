namespace FinancialApp.Core.Application;

public sealed record AppSettings
{
    public string ApplicationName { get; init; } = "TitusBooks";

    public string EnvironmentName { get; init; } = "Development";

    public ApiSettings Api { get; init; } = new();
}
