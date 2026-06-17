using System.Net;
using System.Net.Http.Json;
using FinancialApp.Api.Health;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialApp.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyApiStatus()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("healthy", body.Status);
        Assert.Equal("TitusBooks API", body.Service);
    }

    [Fact]
    public async Task DatabaseHealthEndpoint_ReturnsUnavailableWhenConnectionStringIsMissing()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/database");
        var body = await response.Content.ReadFromJsonAsync<DatabaseHealthResult>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("unhealthy", body.Status);
        Assert.Equal("Database connection string is not configured.", body.Message);
    }
}
