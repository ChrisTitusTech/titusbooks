namespace FinancialApp.Api.Health;

public sealed record DatabaseHealthResult(string Status, string? Message)
{
    public static DatabaseHealthResult Healthy() => new("healthy", null);

    public static DatabaseHealthResult Unhealthy(string message) => new("unhealthy", message);
}
