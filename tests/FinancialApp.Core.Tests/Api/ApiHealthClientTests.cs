using System.Net;
using FinancialApp.Core.Api;

namespace FinancialApp.Core.Tests.Api;

public sealed class ApiHealthClientTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthyWhenApiReportsHealthy()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """{"status":"healthy","service":"TitusBooks API","checkedAt":"2026-06-17T00:00:00+00:00"}"""))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };
        var client = new ApiHealthClient(httpClient);

        var status = await client.CheckHealthAsync();

        Assert.True(status.IsHealthy);
        Assert.Equal("TitusBooks API is reachable.", status.Message);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthyWhenApiIsUnavailable()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, "{}"))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };
        var client = new ApiHealthClient(httpClient);

        var status = await client.CheckHealthAsync();

        Assert.False(status.IsHealthy);
        Assert.Equal("API returned HTTP 503.", status.Message);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string content;

        public StubHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            this.statusCode = statusCode;
            this.content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
        }
    }
}
