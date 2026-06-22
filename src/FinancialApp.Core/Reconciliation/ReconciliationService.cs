using FinancialApp.Core.Accounting;

namespace FinancialApp.Core.Reconciliation;

public sealed class ReconciliationService
{
    private static readonly AccountType[] ReconcilableAccountTypes =
    [
        AccountType.Asset,
        AccountType.Liability,
        AccountType.Equity
    ];

    private readonly IAccountRepository accountRepository;
    private readonly IReconciliationRepository reconciliationRepository;

    public ReconciliationService(
        IAccountRepository accountRepository,
        IReconciliationRepository reconciliationRepository)
    {
        this.accountRepository = accountRepository;
        this.reconciliationRepository = reconciliationRepository;
    }

    public async Task<ReconciliationPreview> PreviewAsync(
        Guid organizationId,
        Guid accountId,
        DateOnly statementEndDate,
        decimal statementEndBalance,
        IReadOnlyCollection<Guid>? clearedJournalLineIds = null,
        CancellationToken cancellationToken = default)
    {
        var account = await GetReconcilableAccountAsync(
            organizationId,
            accountId,
            cancellationToken);
        var transactions = await reconciliationRepository.ListTransactionsAsync(
            organizationId,
            accountId,
            statementEndDate,
            cancellationToken);
        var selectedIds = (clearedJournalLineIds ?? [])
            .Distinct()
            .ToHashSet();
        var selectableIds = transactions
            .Where(transaction => !transaction.IsReconciled)
            .Select(transaction => transaction.JournalLineId)
            .ToHashSet();

        if (!selectedIds.IsSubsetOf(selectableIds))
        {
            throw new ReconciliationException(
                "One or more selected transactions cannot be cleared for this reconciliation.");
        }

        var clearedBalance = transactions
            .Where(transaction =>
                transaction.IsReconciled
                || selectedIds.Contains(transaction.JournalLineId))
            .Sum(transaction => GetSignedAmount(account.AccountType, transaction));
        var difference = statementEndBalance - clearedBalance;

        return new ReconciliationPreview(
            accountId,
            statementEndDate,
            statementEndBalance,
            clearedBalance,
            difference,
            transactions);
    }

    public async Task<ReconciliationPreview> CompleteAsync(
        Guid organizationId,
        Guid accountId,
        DateOnly statementEndDate,
        decimal statementEndBalance,
        IReadOnlyCollection<Guid> clearedJournalLineIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clearedJournalLineIds);

        var preview = await PreviewAsync(
            organizationId,
            accountId,
            statementEndDate,
            statementEndBalance,
            clearedJournalLineIds,
            cancellationToken);
        if (preview.Difference != 0)
        {
            throw new ReconciliationException(
                "Reconciliation cannot be completed until the difference is zero.");
        }

        var reconciliation = new Reconciliation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            AccountId = accountId,
            StatementEndDate = statementEndDate,
            StatementEndBalance = statementEndBalance,
            CompletedAt = DateTimeOffset.UtcNow
        };
        await reconciliationRepository.CompleteAsync(
            reconciliation,
            clearedJournalLineIds.Distinct().ToList(),
            cancellationToken);

        var completedIds = clearedJournalLineIds.ToHashSet();
        return preview with
        {
            Transactions = preview.Transactions
                .Select(transaction => completedIds.Contains(transaction.JournalLineId)
                    ? transaction with { ReconciliationId = reconciliation.Id }
                    : transaction)
                .ToList()
        };
    }

    private async Task<Account> GetReconcilableAccountAsync(
        Guid organizationId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await accountRepository.GetByIdAsync(
            organizationId,
            accountId,
            cancellationToken)
            ?? throw new ReconciliationException("Account was not found for this organization.");

        if (!ReconcilableAccountTypes.Contains(account.AccountType))
        {
            throw new ReconciliationException(
                "Only asset, liability, and equity accounts can be reconciled.");
        }

        return account;
    }

    private static decimal GetSignedAmount(
        AccountType accountType,
        ReconciliationTransaction transaction)
    {
        return accountType == AccountType.Asset
            ? transaction.Debit - transaction.Credit
            : transaction.Credit - transaction.Debit;
    }
}
