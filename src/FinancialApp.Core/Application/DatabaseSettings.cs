namespace FinancialApp.Core.Application;

public sealed record DatabaseSettings
{
    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 5432;

    public string DatabaseName { get; init; } = "titusbooks";

    public string SslMode { get; init; } = "Prefer";
}
