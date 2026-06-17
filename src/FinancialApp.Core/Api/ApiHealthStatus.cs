namespace FinancialApp.Core.Api;

public sealed record ApiHealthStatus(bool IsHealthy, string Message)
{
    public static ApiHealthStatus Healthy(string? serviceName)
    {
        return new ApiHealthStatus(true, $"{serviceName ?? "API"} is reachable.");
    }

    public static ApiHealthStatus Unhealthy(string message)
    {
        return new ApiHealthStatus(false, message);
    }
}
