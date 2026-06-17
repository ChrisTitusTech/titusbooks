using FinancialApp.Api.Health;
using Microsoft.AspNetCore.Mvc;

namespace FinancialApp.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", () => Results.Redirect("/health"));

        endpoints.MapGet("/health", () =>
            Results.Ok(new HealthResponse("healthy", "TitusBooks API", DateTimeOffset.UtcNow)));

        endpoints.MapGet("/health/database", async (
            [FromServices] DatabaseHealthCheck healthCheck,
            CancellationToken cancellationToken) =>
        {
            var result = await healthCheck.CheckAsync(cancellationToken);
            return result.Status == "healthy"
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        return endpoints;
    }
}
