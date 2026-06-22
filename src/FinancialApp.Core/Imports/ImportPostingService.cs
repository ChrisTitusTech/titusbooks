using FinancialApp.Core.Accounting;

namespace FinancialApp.Core.Imports;

public sealed class ImportPostingService
{
    private static readonly AccountType[] BalanceSheetAccountTypes =
    [
        AccountType.Asset,
        AccountType.Liability,
        AccountType.Equity
    ];

    private readonly IAccountRepository accountRepository;
    private readonly IImportPostingRepository postingRepository;

    public ImportPostingService(
        IAccountRepository accountRepository,
        IImportPostingRepository postingRepository)
    {
        this.accountRepository = accountRepository;
        this.postingRepository = postingRepository;
    }

    public async Task<ImportPostingResult> PostAsync(
        Guid organizationId,
        Guid sourceAccountId,
        IReadOnlyCollection<Guid> transactionIds,
        Guid? merchantFeeAccountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactionIds);

        var distinctIds = transactionIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            throw new ImportPostingException("Select at least one imported transaction.");
        }

        var sourceAccount = await GetRequiredAccountAsync(
            organizationId,
            sourceAccountId,
            cancellationToken);
        EnsureActive(sourceAccount);
        if (!BalanceSheetAccountTypes.Contains(sourceAccount.AccountType))
        {
            throw new ImportPostingException(
                "Posting account must be an asset, liability, or equity account.");
        }

        var transactions = await postingRepository.ListForPostingAsync(
            organizationId,
            distinctIds,
            cancellationToken);
        if (transactions.Count != distinctIds.Count)
        {
            throw new ImportPostingException(
                "Every selected transaction must exist and be categorized.");
        }

        var entries = new List<JournalEntry>(transactions.Count);
        Account? merchantFeeAccount = null;
        foreach (var transaction in transactions)
        {
            if (transaction.Status != ImportedTransactionStatus.Categorized
                || transaction.CategoryAccountId is null)
            {
                throw new ImportPostingException(
                    "Every selected transaction must be categorized before posting.");
            }

            if (transaction.Amount == 0)
            {
                throw new ImportPostingException("Zero-amount transactions cannot be posted.");
            }

            var categoryAccount = await GetRequiredAccountAsync(
                organizationId,
                transaction.CategoryAccountId.Value,
                cancellationToken);
            EnsureActive(categoryAccount);
            if (RequiresPayPalSplit(transaction))
            {
                if (merchantFeeAccountId is null)
                {
                    throw new ImportPostingException(
                        "Select a merchant fee expense account for PayPal sales.");
                }

                merchantFeeAccount ??= await GetRequiredAccountAsync(
                    organizationId,
                    merchantFeeAccountId.Value,
                    cancellationToken);
                EnsureActive(merchantFeeAccount);
                if (merchantFeeAccount.AccountType != AccountType.Expense)
                {
                    throw new ImportPostingException(
                        "Merchant fee account must be an expense account.");
                }
            }

            entries.Add(CreateEntry(
                transaction,
                sourceAccount,
                categoryAccount,
                merchantFeeAccount));
        }

        await postingRepository.PostAsync(organizationId, entries, cancellationToken);
        return new ImportPostingResult(
            entries.Count,
            entries.Select(entry => entry.Id).ToList());
    }

    private static JournalEntry CreateEntry(
        ImportedTransaction transaction,
        Account sourceAccount,
        Account categoryAccount,
        Account? merchantFeeAccount)
    {
        if (sourceAccount.Id == categoryAccount.Id)
        {
            throw new ImportPostingException(
                "Posting account and category account must be different.");
        }

        var amount = Math.Abs(transaction.Amount);
        var memo = string.IsNullOrWhiteSpace(transaction.Description)
            ? null
            : transaction.Description.Trim();
        IReadOnlyList<JournalLineDraft> lines;
        if (RequiresPayPalSplit(transaction))
        {
            if (sourceAccount.AccountType != AccountType.Asset
                || categoryAccount.AccountType != AccountType.Income
                || merchantFeeAccount is null)
            {
                throw new ImportPostingException(
                    "PayPal sales require an asset posting account, income category, and merchant fee expense account.");
            }

            var gross = transaction.GrossAmount!.Value;
            var fee = transaction.FeeAmount!.Value;
            var net = transaction.NetAmount!.Value;
            if (gross != net + fee)
            {
                throw new ImportPostingException(
                    "PayPal gross amount must equal net amount plus fee.");
            }

            lines =
            [
                new JournalLineDraft
                {
                    AccountId = sourceAccount.Id,
                    Debit = net,
                    Memo = memo
                },
                new JournalLineDraft
                {
                    AccountId = merchantFeeAccount.Id,
                    Debit = fee,
                    Memo = memo
                },
                new JournalLineDraft
                {
                    AccountId = categoryAccount.Id,
                    Credit = gross,
                    Memo = memo
                }
            ];
        }
        else
        {
            lines = categoryAccount.AccountType switch
        {
            AccountType.Expense => transaction.Amount < 0
                ? Lines(categoryAccount.Id, sourceAccount.Id, amount, memo)
                : Lines(sourceAccount.Id, categoryAccount.Id, amount, memo),
            AccountType.Income => transaction.Amount > 0
                ? Lines(sourceAccount.Id, categoryAccount.Id, amount, memo)
                : Lines(categoryAccount.Id, sourceAccount.Id, amount, memo),
            AccountType.Asset or AccountType.Liability or AccountType.Equity =>
                transaction.Amount > 0
                    ? Lines(sourceAccount.Id, categoryAccount.Id, amount, memo)
                    : Lines(categoryAccount.Id, sourceAccount.Id, amount, memo),
            _ => throw new ImportPostingException("Category account type is not supported.")
        };
        }
        var entry = new JournalEntryDraft
        {
            OrganizationId = transaction.OrganizationId,
            EntryDate = transaction.PostedDate,
            Memo = memo,
            SourceImportedTransactionId = transaction.Id,
            Lines = lines
        }.ToJournalEntry();
        entry.EnsureBalanced();
        return entry;
    }

    private static bool RequiresPayPalSplit(ImportedTransaction transaction)
    {
        return transaction.Kind == ImportedTransactionKind.Payment
            && transaction.GrossAmount > 0
            && transaction.FeeAmount > 0
            && transaction.NetAmount > 0;
    }

    private static IReadOnlyList<JournalLineDraft> Lines(
        Guid debitAccountId,
        Guid creditAccountId,
        decimal amount,
        string? memo)
    {
        return
        [
            new JournalLineDraft
            {
                AccountId = debitAccountId,
                Debit = amount,
                Memo = memo
            },
            new JournalLineDraft
            {
                AccountId = creditAccountId,
                Credit = amount,
                Memo = memo
            }
        ];
    }

    private async Task<Account> GetRequiredAccountAsync(
        Guid organizationId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        return await accountRepository.GetByIdAsync(
            organizationId,
            accountId,
            cancellationToken)
            ?? throw new ImportPostingException(
                "Account was not found for this organization.");
    }

    private static void EnsureActive(Account account)
    {
        if (!account.IsActive)
        {
            throw new ImportPostingException("Account must be active.");
        }
    }
}
