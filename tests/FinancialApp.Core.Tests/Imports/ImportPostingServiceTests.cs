using FinancialApp.Core.Accounting;
using FinancialApp.Core.Imports;

namespace FinancialApp.Core.Tests.Imports;

public sealed class ImportPostingServiceTests
{
    private readonly Guid organizationId = Guid.NewGuid();
    private readonly Guid checkingAccountId = Guid.NewGuid();
    private readonly Guid expenseAccountId = Guid.NewGuid();
    private readonly Guid incomeAccountId = Guid.NewGuid();
    private readonly Guid merchantFeeAccountId = Guid.NewGuid();

    [Fact]
    public async Task PostAsync_PostsExpenseAndIncomeAsBalancedEntries()
    {
        var expense = Transaction(-42m, expenseAccountId);
        var income = Transaction(500m, incomeAccountId);
        var postingRepository = new InMemoryPostingRepository([expense, income]);
        var service = CreateService(postingRepository);

        var result = await service.PostAsync(
            organizationId,
            checkingAccountId,
            [expense.Id, income.Id]);

        Assert.Equal(2, result.PostedCount);
        Assert.Equal(2, postingRepository.Entries.Count);
        Assert.Equal(expenseAccountId, postingRepository.ExpectedCategoryAccountIds[expense.Id]);
        Assert.Equal(incomeAccountId, postingRepository.ExpectedCategoryAccountIds[income.Id]);
        Assert.All(postingRepository.Entries, entry => Assert.True(entry.IsBalanced));
        Assert.Contains(
            postingRepository.Entries.Single(entry =>
                entry.SourceImportedTransactionId == expense.Id).Lines,
            line => line.AccountId == expenseAccountId && line.Debit == 42m);
        Assert.Contains(
            postingRepository.Entries.Single(entry =>
                entry.SourceImportedTransactionId == income.Id).Lines,
            line => line.AccountId == incomeAccountId && line.Credit == 500m);
    }

    [Fact]
    public async Task PostAsync_HandlesExpenseRefund()
    {
        var refund = Transaction(15m, expenseAccountId);
        var postingRepository = new InMemoryPostingRepository([refund]);
        var service = CreateService(postingRepository);

        await service.PostAsync(
            organizationId,
            checkingAccountId,
            [refund.Id]);

        var entry = Assert.Single(postingRepository.Entries);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == checkingAccountId && line.Debit == 15m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == expenseAccountId && line.Credit == 15m);
    }

    [Fact]
    public async Task PostAsync_RejectsPendingTransaction()
    {
        var pending = Transaction(-42m, expenseAccountId) with
        {
            Status = ImportedTransactionStatus.Pending
        };
        var postingRepository = new InMemoryPostingRepository([pending]);
        var service = CreateService(postingRepository);

        await Assert.ThrowsAsync<ImportPostingException>(() =>
            service.PostAsync(organizationId, checkingAccountId, [pending.Id]));

        Assert.Empty(postingRepository.Entries);
    }

    [Fact]
    public async Task PostAsync_PostsPayPalGrossFeeAndNetSplit()
    {
        var sale = Transaction(96.51m, incomeAccountId) with
        {
            Kind = ImportedTransactionKind.Payment,
            GrossAmount = 100m,
            FeeAmount = 3.49m,
            NetAmount = 96.51m
        };
        var postingRepository = new InMemoryPostingRepository([sale]);
        var service = CreateService(postingRepository);

        await service.PostAsync(
            organizationId,
            checkingAccountId,
            [sale.Id],
            merchantFeeAccountId);

        var entry = Assert.Single(postingRepository.Entries);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == checkingAccountId && line.Debit == 96.51m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == merchantFeeAccountId && line.Debit == 3.49m);
        Assert.Contains(entry.Lines, line =>
            line.AccountId == incomeAccountId && line.Credit == 100m);
    }

    private ImportPostingService CreateService(InMemoryPostingRepository postingRepository)
    {
        var accounts = new InMemoryAccountRepository(
        [
            Account(checkingAccountId, "Checking", AccountType.Asset),
            Account(expenseAccountId, "Office Supplies", AccountType.Expense),
            Account(incomeAccountId, "Consulting Income", AccountType.Income),
            Account(merchantFeeAccountId, "Merchant Fees", AccountType.Expense)
        ]);
        return new ImportPostingService(accounts, postingRepository);
    }

    private ImportedTransaction Transaction(decimal amount, Guid categoryAccountId)
    {
        return new ImportedTransaction
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Source = "Generic CSV",
            PostedDate = new DateOnly(2026, 6, 20),
            Description = "Fake transaction",
            Amount = amount,
            Status = ImportedTransactionStatus.Categorized,
            CategoryAccountId = categoryAccountId,
            Fingerprint = Guid.NewGuid().ToString("N")
        };
    }

    private Account Account(Guid id, string name, AccountType accountType)
    {
        return new Account
        {
            Id = id,
            OrganizationId = organizationId,
            Name = name,
            AccountType = accountType
        };
    }

    private sealed class InMemoryPostingRepository(
        IReadOnlyList<ImportedTransaction> transactions) : IImportPostingRepository
    {
        public List<JournalEntry> Entries { get; } = [];

        public IReadOnlyDictionary<Guid, Guid> ExpectedCategoryAccountIds { get; private set; }
            = new Dictionary<Guid, Guid>();

        public Task<IReadOnlyList<ImportedTransaction>> ListForPostingAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid> transactionIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ImportedTransaction>>(
                transactions
                    .Where(transaction =>
                        transaction.OrganizationId == organizationId
                        && transactionIds.Contains(transaction.Id))
                    .ToList());
        }

        public Task PostAsync(
            Guid organizationId,
            IReadOnlyCollection<JournalEntry> journalEntries,
            IReadOnlyDictionary<Guid, Guid> expectedCategoryAccountIds,
            CancellationToken cancellationToken = default)
        {
            Entries.AddRange(journalEntries);
            ExpectedCategoryAccountIds = expectedCategoryAccountIds;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAccountRepository(
        IReadOnlyList<Account> accounts) : IAccountRepository
    {
        public Task<Account?> GetByIdAsync(
            Guid organizationId,
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(accounts.SingleOrDefault(account =>
                account.OrganizationId == organizationId && account.Id == accountId));
        }

        public Task<Account?> FindByNameAsync(
            Guid organizationId,
            string name,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Account?>(null);
        }

        public Task<IReadOnlyList<Account>> ListByOrganizationAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Account>>(
                accounts.Where(account => account.OrganizationId == organizationId).ToList());
        }

        public Task AddAsync(Account account, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeactivateAsync(
            Guid organizationId,
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
