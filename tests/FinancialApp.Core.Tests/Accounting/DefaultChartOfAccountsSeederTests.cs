using FinancialApp.Core.Accounting;

namespace FinancialApp.Core.Tests.Accounting;

public sealed class DefaultChartOfAccountsSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesDefaultChartOfAccounts()
    {
        var organizationId = Guid.NewGuid();
        var repository = new InMemoryAccountRepository();
        var seeder = new DefaultChartOfAccountsSeeder(repository);

        var createdAccounts = await seeder.SeedAsync(organizationId);

        Assert.Equal(DefaultChartOfAccounts.Templates.Count, createdAccounts.Count);
        Assert.Contains(createdAccounts, account => account.Name == "Checking" && account.AccountType == AccountType.Asset);
        Assert.Contains(createdAccounts, account => account.Name == "Merchant Fees" && account.AccountType == AccountType.Expense);
        Assert.All(createdAccounts, account => Assert.Equal(organizationId, account.OrganizationId));
    }

    [Fact]
    public async Task SeedAsync_DoesNotCreateDuplicateDefaultAccounts()
    {
        var organizationId = Guid.NewGuid();
        var repository = new InMemoryAccountRepository();
        var seeder = new DefaultChartOfAccountsSeeder(repository);

        var firstRun = await seeder.SeedAsync(organizationId);
        var secondRun = await seeder.SeedAsync(organizationId);
        var allAccounts = await repository.ListByOrganizationAsync(organizationId);

        Assert.Equal(DefaultChartOfAccounts.Templates.Count, firstRun.Count);
        Assert.Empty(secondRun);
        Assert.Equal(DefaultChartOfAccounts.Templates.Count, allAccounts.Count);
    }

    private sealed class InMemoryAccountRepository : IAccountRepository
    {
        private readonly List<Account> accounts = [];

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

        public Task<Account?> GetByIdAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken = default)
        {
            var account = accounts.SingleOrDefault(account =>
                account.OrganizationId == organizationId
                && account.Id == accountId);

            return Task.FromResult(account);
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

        public Task<IReadOnlyList<Account>> ListByOrganizationAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Account>>(
                accounts.Where(account => account.OrganizationId == organizationId).ToList());
        }
    }
}
