namespace FinancialApp.Core.Accounting;

public sealed class DefaultChartOfAccountsSeeder
{
    private readonly IAccountRepository accountRepository;

    public DefaultChartOfAccountsSeeder(IAccountRepository accountRepository)
    {
        this.accountRepository = accountRepository;
    }

    public async Task<IReadOnlyList<Account>> SeedAsync(
        Guid organizationId,
        string currency = "USD",
        CancellationToken cancellationToken = default)
    {
        var createdAccounts = new List<Account>();

        foreach (var template in DefaultChartOfAccounts.Templates)
        {
            var existingAccount = await accountRepository.FindByNameAsync(
                organizationId,
                template.Name,
                cancellationToken);

            if (existingAccount is not null)
            {
                continue;
            }

            var account = new Account
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = template.Name,
                AccountType = template.AccountType,
                AccountSubtype = template.AccountSubtype,
                Currency = string.IsNullOrWhiteSpace(template.Currency) ? currency : template.Currency
            };

            await accountRepository.AddAsync(account, cancellationToken);
            createdAccounts.Add(account);
        }

        return createdAccounts;
    }
}
