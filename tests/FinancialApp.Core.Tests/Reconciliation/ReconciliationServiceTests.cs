using FinancialApp.Core.Accounting;
using FinancialApp.Core.Reconciliation;
using ReconciliationRecord = FinancialApp.Core.Reconciliation.Reconciliation;

namespace FinancialApp.Core.Tests.Reconciliation;

public sealed class ReconciliationServiceTests
{
    private readonly Guid organizationId = Guid.NewGuid();
    private readonly Guid accountId = Guid.NewGuid();

    [Fact]
    public async Task PreviewAsync_CalculatesAssetClearedBalanceAndDifference()
    {
        var existingReconciliationId = Guid.NewGuid();
        var selectedLineId = Guid.NewGuid();
        var repository = new InMemoryReconciliationRepository(
        [
            Transaction(debit: 100m, reconciliationId: existingReconciliationId),
            Transaction(debit: 50m, journalLineId: selectedLineId),
            Transaction(credit: 20m)
        ]);
        var service = CreateService(AccountType.Asset, repository);

        var preview = await service.PreviewAsync(
            organizationId,
            accountId,
            new DateOnly(2026, 6, 30),
            150m,
            [selectedLineId]);

        Assert.Equal(150m, preview.ClearedBalance);
        Assert.Equal(0m, preview.Difference);
    }

    [Fact]
    public async Task PreviewAsync_UsesCreditNormalBalanceForLiability()
    {
        var selectedLineId = Guid.NewGuid();
        var repository = new InMemoryReconciliationRepository(
        [
            Transaction(credit: 200m, journalLineId: selectedLineId)
        ]);
        var service = CreateService(AccountType.Liability, repository);

        var preview = await service.PreviewAsync(
            organizationId,
            accountId,
            new DateOnly(2026, 6, 30),
            200m,
            [selectedLineId]);

        Assert.Equal(200m, preview.ClearedBalance);
        Assert.Equal(0m, preview.Difference);
    }

    [Fact]
    public async Task CompleteAsync_RejectsNonZeroDifference()
    {
        var selectedLineId = Guid.NewGuid();
        var repository = new InMemoryReconciliationRepository(
        [
            Transaction(debit: 90m, journalLineId: selectedLineId)
        ]);
        var service = CreateService(AccountType.Asset, repository);

        var exception = await Assert.ThrowsAsync<ReconciliationException>(() =>
            service.CompleteAsync(
                organizationId,
                accountId,
                new DateOnly(2026, 6, 30),
                100m,
                [selectedLineId]));

        Assert.Contains("difference is zero", exception.Message);
        Assert.Null(repository.CompletedReconciliation);
    }

    [Fact]
    public async Task CompleteAsync_PersistsSelectedLinesWhenDifferenceIsZero()
    {
        var selectedLineId = Guid.NewGuid();
        var repository = new InMemoryReconciliationRepository(
        [
            Transaction(debit: 125m, journalLineId: selectedLineId)
        ]);
        var service = CreateService(AccountType.Asset, repository);

        var result = await service.CompleteAsync(
            organizationId,
            accountId,
            new DateOnly(2026, 6, 30),
            125m,
            [selectedLineId]);

        Assert.Equal(0m, result.Difference);
        Assert.NotNull(repository.CompletedReconciliation);
        Assert.Equal([selectedLineId], repository.CompletedJournalLineIds);
    }

    [Fact]
    public async Task PreviewAsync_RejectsAlreadyReconciledSelection()
    {
        var reconciledLineId = Guid.NewGuid();
        var repository = new InMemoryReconciliationRepository(
        [
            Transaction(
                debit: 100m,
                journalLineId: reconciledLineId,
                reconciliationId: Guid.NewGuid())
        ]);
        var service = CreateService(AccountType.Asset, repository);

        await Assert.ThrowsAsync<ReconciliationException>(() =>
            service.PreviewAsync(
                organizationId,
                accountId,
                new DateOnly(2026, 6, 30),
                100m,
                [reconciledLineId]));
    }

    private ReconciliationService CreateService(
        AccountType accountType,
        InMemoryReconciliationRepository reconciliationRepository)
    {
        var accountRepository = new InMemoryAccountRepository(new Account
        {
            Id = accountId,
            OrganizationId = organizationId,
            Name = "Statement Account",
            AccountType = accountType
        });
        return new ReconciliationService(accountRepository, reconciliationRepository);
    }

    private ReconciliationTransaction Transaction(
        decimal debit = 0,
        decimal credit = 0,
        Guid? journalLineId = null,
        Guid? reconciliationId = null)
    {
        return new ReconciliationTransaction(
            journalLineId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 15),
            "Test transaction",
            debit,
            credit,
            "Other account",
            reconciliationId);
    }

    private sealed class InMemoryReconciliationRepository(
        IReadOnlyList<ReconciliationTransaction> transactions) : IReconciliationRepository
    {
        public ReconciliationRecord? CompletedReconciliation { get; private set; }

        public IReadOnlyList<Guid> CompletedJournalLineIds { get; private set; } = [];

        public Task<IReadOnlyList<ReconciliationTransaction>> ListTransactionsAsync(
            Guid organizationId,
            Guid accountId,
            DateOnly statementEndDate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(transactions);
        }

        public Task CompleteAsync(
            ReconciliationRecord reconciliation,
            IReadOnlyCollection<Guid> journalLineIds,
            CancellationToken cancellationToken = default)
        {
            CompletedReconciliation = reconciliation;
            CompletedJournalLineIds = journalLineIds.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAccountRepository(Account account) : IAccountRepository
    {
        public Task<Account?> GetByIdAsync(
            Guid organizationId,
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Account?>(
                account.OrganizationId == organizationId && account.Id == accountId
                    ? account
                    : null);
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
            return Task.FromResult<IReadOnlyList<Account>>([account]);
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
