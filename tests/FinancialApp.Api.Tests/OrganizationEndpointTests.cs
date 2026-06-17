using System.Net;
using System.Net.Http.Json;
using FinancialApp.Api.Accounting;
using FinancialApp.Api.Organizations;
using FinancialApp.Core.Accounting;
using FinancialApp.Core.Organizations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialApp.Api.Tests;

public sealed class OrganizationEndpointTests
{
    [Fact]
    public async Task CreateOrganization_CreatesOrganization()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/organizations", new CreateOrganizationRequest("Titus Books", "usd", 1));
        var body = await response.Content.ReadFromJsonAsync<OrganizationResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Titus Books", body.Name);
        Assert.Equal("USD", body.BaseCurrency);
        Assert.Single(repositories.Organizations);
    }

    [Fact]
    public async Task CreateOrganization_RejectsMissingName()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/organizations", new CreateOrganizationRequest("", "USD", 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(repositories.Organizations);
    }

    [Fact]
    public async Task SeedDefaultAccounts_CreatesAccountsOnce()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = Guid.NewGuid();

        var firstResponse = await client.PostAsync($"/organizations/{organizationId}/accounts/defaults", null);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<SeedAccountsResponse>();
        var secondResponse = await client.PostAsync($"/organizations/{organizationId}/accounts/defaults", null);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<SeedAccountsResponse>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstBody);
        Assert.Equal(DefaultChartOfAccounts.Templates.Count, firstBody.CreatedCount);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondBody);
        Assert.Equal(0, secondBody.CreatedCount);
    }

    [Fact]
    public async Task ListAccounts_ReturnsOrganizationAccounts()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = Guid.NewGuid();
        await client.PostAsync($"/organizations/{organizationId}/accounts/defaults", null);

        var response = await client.GetAsync($"/organizations/{organizationId}/accounts");
        var accounts = await response.Content.ReadFromJsonAsync<List<AccountResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(accounts);
        Assert.Contains(accounts, account => account.Name == "Checking");
        Assert.Contains(accounts, account => account.Name == "Merchant Fees");
    }

    private static WebApplicationFactory<Program> CreateFactory(InMemoryRepositories repositories)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IOrganizationRepository>();
                    services.RemoveAll<IAccountRepository>();
                    services.RemoveAll<DefaultChartOfAccountsSeeder>();
                    services.AddSingleton<IOrganizationRepository>(repositories);
                    services.AddSingleton<IAccountRepository>(repositories);
                    services.AddSingleton<DefaultChartOfAccountsSeeder>();
                });
            });
    }

    private sealed class InMemoryRepositories : IOrganizationRepository, IAccountRepository
    {
        private readonly List<Account> accounts = [];

        public List<Organization> Organizations { get; } = [];

        public Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
        {
            Organizations.Add(organization);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Organization>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Organization>>(Organizations.ToList());
        }

        public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var organization = Organizations.SingleOrDefault(organization => organization.Id == id);
            return Task.FromResult(organization);
        }

        public Task<Account?> FindByNameAsync(Guid organizationId, string name, CancellationToken cancellationToken = default)
        {
            var account = accounts.SingleOrDefault(account =>
                account.OrganizationId == organizationId
                && string.Equals(account.Name, name, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(account);
        }

        public Task AddAsync(Account account, CancellationToken cancellationToken = default)
        {
            accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Account>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Account>>(
                accounts.Where(account => account.OrganizationId == organizationId).ToList());
        }
    }
}
