using FinancialApp.Core.Accounting;

namespace FinancialApp.Core.Tests.Accounting;

public sealed class AccountingServiceTests
{
    private readonly Guid organizationId = Guid.NewGuid();
    private readonly Guid checkingAccountId = Guid.NewGuid();
    private readonly Guid savingsAccountId = Guid.NewGuid();
    private readonly Guid creditCardAccountId = Guid.NewGuid();
    private readonly Guid expenseAccountId = Guid.NewGuid();
    private readonly Guid incomeAccountId = Guid.NewGuid();

    [Fact]
    public async Task PostJournalEntryAsync_RejectsUnbalancedEntry()
    {
        var repository = new InMemoryJournalEntryRepository();
        var service = new AccountingService(repository);

        var draft = new JournalEntryDraft
        {
            OrganizationId = organizationId,
            EntryDate = new DateOnly(2026, 6, 16),
            Memo = "Unbalanced expense",
            Lines =
            [
                new JournalLineDraft { AccountId = expenseAccountId, Debit = 42.00m },
                new JournalLineDraft { AccountId = checkingAccountId, Credit = 41.00m }
            ]
        };

        var exception = await Assert.ThrowsAsync<AccountingException>(() => service.PostJournalEntryAsync(draft));

        Assert.Contains("unbalanced", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(repository.Entries);
    }

    [Fact]
    public async Task PostJournalEntryAsync_PersistsBalancedEntry()
    {
        var repository = new InMemoryJournalEntryRepository();
        var service = new AccountingService(repository);

        var draft = new JournalEntryDraft
        {
            OrganizationId = organizationId,
            EntryDate = new DateOnly(2026, 6, 16),
            Memo = "Office supplies",
            Lines =
            [
                new JournalLineDraft { AccountId = expenseAccountId, Debit = 42.00m },
                new JournalLineDraft { AccountId = checkingAccountId, Credit = 42.00m }
            ]
        };

        var entry = await service.PostJournalEntryAsync(draft);

        Assert.True(entry.IsBalanced);
        Assert.Single(repository.Entries);
        Assert.Equal(entry.Id, repository.Entries[0].Id);
        Assert.All(entry.Lines, line => Assert.Equal(entry.Id, line.JournalEntryId));
    }

    [Fact]
    public async Task PostJournalEntryAsync_RejectsLineWithDebitAndCredit()
    {
        var repository = new InMemoryJournalEntryRepository();
        var service = new AccountingService(repository);

        var draft = new JournalEntryDraft
        {
            OrganizationId = organizationId,
            EntryDate = new DateOnly(2026, 6, 16),
            Lines =
            [
                new JournalLineDraft { AccountId = expenseAccountId, Debit = 42.00m, Credit = 1.00m },
                new JournalLineDraft { AccountId = checkingAccountId, Credit = 42.00m }
            ]
        };

        await Assert.ThrowsAsync<AccountingException>(() => service.PostJournalEntryAsync(draft));

        Assert.Empty(repository.Entries);
    }

    [Fact]
    public async Task PostExpenseAsync_DebitsExpenseAndCreditsChecking()
    {
        var journalRepository = new InMemoryJournalEntryRepository();
        var accountRepository = CreateAccountRepository();
        var service = new AccountingService(journalRepository, accountRepository);

        var entry = await service.PostExpenseAsync(new ManualExpense(
            organizationId,
            new DateOnly(2026, 6, 17),
            checkingAccountId,
            expenseAccountId,
            42.00m,
            "Office supplies"));

        Assert.True(entry.IsBalanced);
        Assert.Contains(entry.Lines, line => line.AccountId == expenseAccountId && line.Debit == 42.00m);
        Assert.Contains(entry.Lines, line => line.AccountId == checkingAccountId && line.Credit == 42.00m);
        Assert.Single(journalRepository.Entries);
    }

    [Fact]
    public async Task PostExpenseAsync_AllowsCreditCardLiabilityPaymentAccount()
    {
        var journalRepository = new InMemoryJournalEntryRepository();
        var accountRepository = CreateAccountRepository();
        var service = new AccountingService(journalRepository, accountRepository);

        var entry = await service.PostExpenseAsync(new ManualExpense(
            organizationId,
            new DateOnly(2026, 6, 17),
            creditCardAccountId,
            expenseAccountId,
            19.95m,
            "Software subscription"));

        Assert.True(entry.IsBalanced);
        Assert.Contains(entry.Lines, line => line.AccountId == expenseAccountId && line.Debit == 19.95m);
        Assert.Contains(entry.Lines, line => line.AccountId == creditCardAccountId && line.Credit == 19.95m);
    }

    [Fact]
    public async Task PostIncomeAsync_DebitsCheckingAndCreditsIncome()
    {
        var journalRepository = new InMemoryJournalEntryRepository();
        var accountRepository = CreateAccountRepository();
        var service = new AccountingService(journalRepository, accountRepository);

        var entry = await service.PostIncomeAsync(new ManualIncome(
            organizationId,
            new DateOnly(2026, 6, 17),
            checkingAccountId,
            incomeAccountId,
            250.00m,
            "Consulting income"));

        Assert.True(entry.IsBalanced);
        Assert.Contains(entry.Lines, line => line.AccountId == checkingAccountId && line.Debit == 250.00m);
        Assert.Contains(entry.Lines, line => line.AccountId == incomeAccountId && line.Credit == 250.00m);
    }

    [Fact]
    public async Task PostTransferAsync_CreditsSourceAssetAndDebitsDestinationAsset()
    {
        var journalRepository = new InMemoryJournalEntryRepository();
        var accountRepository = CreateAccountRepository();
        var service = new AccountingService(journalRepository, accountRepository);

        var entry = await service.PostTransferAsync(new ManualTransfer(
            organizationId,
            new DateOnly(2026, 6, 17),
            checkingAccountId,
            savingsAccountId,
            100.00m,
            "Move cash to savings"));

        Assert.True(entry.IsBalanced);
        Assert.Contains(entry.Lines, line => line.AccountId == checkingAccountId && line.Credit == 100.00m);
        Assert.Contains(entry.Lines, line => line.AccountId == savingsAccountId && line.Debit == 100.00m);
    }

    [Fact]
    public async Task PostTransferAsync_DebitsLiabilityWhenPayingCreditCard()
    {
        var journalRepository = new InMemoryJournalEntryRepository();
        var accountRepository = CreateAccountRepository();
        var service = new AccountingService(journalRepository, accountRepository);

        var entry = await service.PostTransferAsync(new ManualTransfer(
            organizationId,
            new DateOnly(2026, 6, 17),
            checkingAccountId,
            creditCardAccountId,
            75.00m,
            "Credit card payment"));

        Assert.True(entry.IsBalanced);
        Assert.Contains(entry.Lines, line => line.AccountId == checkingAccountId && line.Credit == 75.00m);
        Assert.Contains(entry.Lines, line => line.AccountId == creditCardAccountId && line.Debit == 75.00m);
    }

    [Fact]
    public async Task PostIncomeAsync_RejectsExpenseAccountAsIncomeAccount()
    {
        var journalRepository = new InMemoryJournalEntryRepository();
        var accountRepository = CreateAccountRepository();
        var service = new AccountingService(journalRepository, accountRepository);

        await Assert.ThrowsAsync<AccountingException>(() => service.PostIncomeAsync(new ManualIncome(
            organizationId,
            new DateOnly(2026, 6, 17),
            checkingAccountId,
            expenseAccountId,
            250.00m)));

        Assert.Empty(journalRepository.Entries);
    }

    [Fact]
    public async Task PostExpenseAsync_RejectsZeroAmount()
    {
        var journalRepository = new InMemoryJournalEntryRepository();
        var accountRepository = CreateAccountRepository();
        var service = new AccountingService(journalRepository, accountRepository);

        await Assert.ThrowsAsync<AccountingException>(() => service.PostExpenseAsync(new ManualExpense(
            organizationId,
            new DateOnly(2026, 6, 17),
            checkingAccountId,
            expenseAccountId,
            0m)));

        Assert.Empty(journalRepository.Entries);
    }

    private InMemoryAccountRepository CreateAccountRepository()
    {
        return new InMemoryAccountRepository(
        [
            CreateAccount(checkingAccountId, "Checking", AccountType.Asset, "Checking"),
            CreateAccount(savingsAccountId, "Savings", AccountType.Asset, "Savings"),
            CreateAccount(creditCardAccountId, "Credit Card", AccountType.Liability, "Credit Card"),
            CreateAccount(expenseAccountId, "Office Supplies", AccountType.Expense),
            CreateAccount(incomeAccountId, "Consulting Income", AccountType.Income)
        ]);
    }

    private Account CreateAccount(Guid accountId, string name, AccountType accountType, string? accountSubtype = null)
    {
        return new Account
        {
            Id = accountId,
            OrganizationId = organizationId,
            Name = name,
            AccountType = accountType,
            AccountSubtype = accountSubtype
        };
    }

    private sealed class InMemoryJournalEntryRepository : IJournalEntryRepository
    {
        public List<JournalEntry> Entries { get; } = [];

        public Task AddAsync(JournalEntry journalEntry, CancellationToken cancellationToken = default)
        {
            Entries.Add(journalEntry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AccountRegisterEntry>> ListRegisterAsync(
            Guid organizationId,
            Guid accountId,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            CancellationToken cancellationToken = default)
        {
            var register = Entries
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
                        string.Empty)))
                .ToList();

            return Task.FromResult<IReadOnlyList<AccountRegisterEntry>>(register);
        }
    }

    private sealed class InMemoryAccountRepository : IAccountRepository
    {
        private readonly List<Account> accounts;

        public InMemoryAccountRepository(IEnumerable<Account> accounts)
        {
            this.accounts = accounts.ToList();
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
            var index = accounts.FindIndex(existingAccount => existingAccount.Id == account.Id);
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
