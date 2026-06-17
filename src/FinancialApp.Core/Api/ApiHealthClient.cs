using System.Net.Http.Json;

namespace FinancialApp.Core.Api;

public sealed class ApiHealthClient
{
    private readonly HttpClient httpClient;

    public ApiHealthClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<ApiHealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("/health", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiHealthStatus.Unhealthy($"API returned HTTP {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<ApiHealthResponse>(cancellationToken);

            if (body is not null && string.Equals(body.Status, "healthy", StringComparison.OrdinalIgnoreCase))
            {
                return ApiHealthStatus.Healthy(body.Service);
            }

            return ApiHealthStatus.Unhealthy("API health response was not healthy.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return ApiHealthStatus.Unhealthy("API is unreachable.");
        }
    }
}
