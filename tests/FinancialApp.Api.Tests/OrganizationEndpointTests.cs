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
        var organizationId = await CreateOrganizationAsync(client);

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
        var organizationId = await CreateOrganizationAsync(client);
        await client.PostAsync($"/organizations/{organizationId}/accounts/defaults", null);

        var response = await client.GetAsync($"/organizations/{organizationId}/accounts");
        var accounts = await response.Content.ReadFromJsonAsync<List<AccountResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(accounts);
        Assert.Contains(accounts, account => account.Name == "Checking");
        Assert.Contains(accounts, account => account.Name == "Merchant Fees");
    }

    [Fact]
    public async Task CreateAccount_CreatesAccount()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            new CreateAccountRequest("Bank Savings", "Asset", "Savings"));
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(account);
        Assert.Equal("Bank Savings", account.Name);
        Assert.Equal("Asset", account.AccountType);
    }

    [Fact]
    public async Task CreateAccount_ReturnsNotFoundForMissingOrganization()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/organizations/{Guid.NewGuid()}/accounts",
            new CreateAccountRequest("Bank Savings", "Asset", "Savings"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAccount_RenamesAccount()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var createResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            new CreateAccountRequest("Old Name", "Expense"));
        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var updateResponse = await client.PutAsJsonAsync(
            $"/organizations/{organizationId}/accounts/{createdAccount!.Id}",
            new UpdateAccountRequest("New Name", "Office"));
        var updatedAccount = await updateResponse.Content.ReadFromJsonAsync<AccountResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updatedAccount);
        Assert.Equal("New Name", updatedAccount.Name);
        Assert.Equal("Office", updatedAccount.AccountSubtype);
    }

    [Fact]
    public async Task UpdateAccount_RejectsDuplicateName()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            new CreateAccountRequest("Meals", "Expense"));
        var createResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            new CreateAccountRequest("Travel", "Expense"));
        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var updateResponse = await client.PutAsJsonAsync(
            $"/organizations/{organizationId}/accounts/{createdAccount!.Id}",
            new UpdateAccountRequest("Meals"));

        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
    }

    [Fact]
    public async Task DeactivateAccount_MarksAccountInactive()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var createResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            new CreateAccountRequest("Temporary", "Expense"));
        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var deactivateResponse = await client.PostAsync(
            $"/organizations/{organizationId}/accounts/{createdAccount!.Id}/deactivate",
            null);
        var deactivatedAccount = await deactivateResponse.Content.ReadFromJsonAsync<AccountResponse>();

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        Assert.NotNull(deactivatedAccount);
        Assert.False(deactivatedAccount.IsActive);
    }

    [Fact]
    public async Task PostExpense_CreatesBalancedJournalEntry()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var checking = await CreateAccountAsync(client, organizationId, "Checking", "Asset", "Checking");
        var supplies = await CreateAccountAsync(client, organizationId, "Office Supplies", "Expense");

        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/expenses",
            new PostExpenseRequest(
                new DateOnly(2026, 6, 17),
                checking.Id,
                supplies.Id,
                42.00m,
                "Office supplies"));
        var entry = await response.Content.ReadFromJsonAsync<JournalEntryResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(entry);
        Assert.Equal(42.00m, entry.TotalDebits);
        Assert.Equal(42.00m, entry.TotalCredits);
        Assert.Contains(entry.Lines, line => line.AccountId == supplies.Id && line.Debit == 42.00m);
        Assert.Contains(entry.Lines, line => line.AccountId == checking.Id && line.Credit == 42.00m);
    }

    [Fact]
    public async Task PostIncome_CreatesBalancedJournalEntry()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var checking = await CreateAccountAsync(client, organizationId, "Checking", "Asset", "Checking");
        var consulting = await CreateAccountAsync(client, organizationId, "Consulting Income", "Income");

        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/income",
            new PostIncomeRequest(
                new DateOnly(2026, 6, 17),
                checking.Id,
                consulting.Id,
                250.00m,
                "Consulting income"));
        var entry = await response.Content.ReadFromJsonAsync<JournalEntryResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(entry);
        Assert.Equal(250.00m, entry.TotalDebits);
        Assert.Equal(250.00m, entry.TotalCredits);
        Assert.Contains(entry.Lines, line => line.AccountId == checking.Id && line.Debit == 250.00m);
        Assert.Contains(entry.Lines, line => line.AccountId == consulting.Id && line.Credit == 250.00m);
    }

    [Fact]
    public async Task PostTransfer_RejectsSameAccount()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var checking = await CreateAccountAsync(client, organizationId, "Checking", "Asset", "Checking");

        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/transfers",
            new PostTransferRequest(
                new DateOnly(2026, 6, 17),
                checking.Id,
                checking.Id,
                25.00m,
                "Invalid transfer"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListRegister_ReturnsPostedAccountEntries()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var checking = await CreateAccountAsync(client, organizationId, "Checking", "Asset", "Checking");
        var supplies = await CreateAccountAsync(client, organizationId, "Office Supplies", "Expense");
        await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/expenses",
            new PostExpenseRequest(
                new DateOnly(2026, 6, 17),
                checking.Id,
                supplies.Id,
                42.00m,
                "Office supplies"));

        var response = await client.GetAsync($"/organizations/{organizationId}/accounts/{checking.Id}/register");
        var register = await response.Content.ReadFromJsonAsync<List<AccountRegisterEntryResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(register);
        var entry = Assert.Single(register);
        Assert.Equal(42.00m, entry.Credit);
        Assert.Equal("Office Supplies", entry.OtherAccounts);
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/organizations",
            new CreateOrganizationRequest($"Test Organization {Guid.NewGuid():N}", "USD", 1));
        var organization = await response.Content.ReadFromJsonAsync<OrganizationResponse>();
        return organization!.Id;
    }

    private static async Task<AccountResponse> CreateAccountAsync(
        HttpClient client,
        Guid organizationId,
        string name,
        string accountType,
        string? accountSubtype = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            new CreateAccountRequest(name, accountType, accountSubtype));
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>();
        return account!;
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
                    services.RemoveAll<IJournalEntryRepository>();
                    services.RemoveAll<DefaultChartOfAccountsSeeder>();
                    services.RemoveAll<AccountingService>();
                    services.AddSingleton<IOrganizationRepository>(repositories);
                    services.AddSingleton<IAccountRepository>(repositories);
                    services.AddSingleton<IJournalEntryRepository>(repositories);
                    services.AddSingleton<DefaultChartOfAccountsSeeder>();
                    services.AddSingleton<AccountingService>();
                });
            });
    }

    private sealed class InMemoryRepositories : IOrganizationRepository, IAccountRepository, IJournalEntryRepository
    {
        private readonly List<Account> accounts = [];
        private readonly List<JournalEntry> journalEntries = [];

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

        public Task<Account?> GetByIdAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken = default)
        {
            var account = accounts.SingleOrDefault(account =>
                account.OrganizationId == organizationId
                && account.Id == accountId);

            return Task.FromResult(account);
        }

        public Task AddAsync(Account account, CancellationToken cancellationToken = default)
        {
            accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
        {
            var index = accounts.FindIndex(existingAccount =>
                existingAccount.OrganizationId == account.OrganizationId
                && existingAccount.Id == account.Id);

            if (index >= 0)
            {
                accounts[index] = account;
            }

            return Task.CompletedTask;
        }

        public Task DeactivateAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken = default)
        {
            var index = accounts.FindIndex(account =>
                account.OrganizationId == organizationId
                && account.Id == accountId);

            if (index >= 0)
            {
                accounts[index] = accounts[index] with { IsActive = false };
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Account>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Account>>(
                accounts.Where(account => account.OrganizationId == organizationId).ToList());
        }

        public Task AddAsync(JournalEntry journalEntry, CancellationToken cancellationToken = default)
        {
            journalEntries.Add(journalEntry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AccountRegisterEntry>> ListRegisterAsync(
            Guid organizationId,
            Guid accountId,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            CancellationToken cancellationToken = default)
        {
            var registerEntries = journalEntries
                .Where(entry => entry.OrganizationId == organizationId)
                .Where(entry => startDate is null || entry.EntryDate >= startDate)
                .Where(entry => endDate is null || entry.EntryDate <= endDate)
                .SelectMany(entry => entry.Lines
                    .Where(line => line.AccountId == accountId)
                    .Select(line => new AccountRegisterEntry(
                        entry.Id,
                        entry.EntryDate,
                        entry.Memo,
                        line.Debit,
                        line.Credit,
                        string.Join(", ", entry.Lines
                            .Where(otherLine => otherLine.AccountId != accountId)
                            .Select(otherLine => accounts.Single(account => account.Id == otherLine.AccountId).Name)))))
                .ToList();

            return Task.FromResult<IReadOnlyList<AccountRegisterEntry>>(registerEntries);
        }
    }
}
