namespace FinancialApp.Core.Accounting;

public sealed class AccountingService
{
    private static readonly AccountType[] BalanceSheetAccountTypes =
    [
        AccountType.Asset,
        AccountType.Liability,
        AccountType.Equity
    ];

    private readonly IJournalEntryRepository journalEntryRepository;
    private readonly IAccountRepository? accountRepository;

    public AccountingService(
        IJournalEntryRepository journalEntryRepository,
        IAccountRepository? accountRepository = null)
    {
        this.journalEntryRepository = journalEntryRepository;
        this.accountRepository = accountRepository;
    }

    public async Task<JournalEntry> PostJournalEntryAsync(
        JournalEntryDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var entry = draft.ToJournalEntry();
        entry.EnsureBalanced();

        await journalEntryRepository.AddAsync(entry, cancellationToken);
        return entry;
    }

    public async Task<JournalEntry> PostExpenseAsync(
        ManualExpense expense,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expense);
        ValidatePositiveAmount(expense.Amount);

        var paymentAccount = await GetRequiredAccountAsync(
            expense.OrganizationId,
            expense.PaymentAccountId,
            cancellationToken);
        var expenseAccount = await GetRequiredAccountAsync(
            expense.OrganizationId,
            expense.ExpenseAccountId,
            cancellationToken);

        EnsureActive(paymentAccount);
        EnsureActive(expenseAccount);
        EnsureAccountType(expenseAccount, AccountType.Expense, "Expense account must be an Expense account.");
        EnsureAnyAccountType(
            paymentAccount,
            [AccountType.Asset, AccountType.Liability],
            "Payment account must be an Asset or Liability account.");

        return await PostJournalEntryAsync(new JournalEntryDraft
        {
            OrganizationId = expense.OrganizationId,
            EntryDate = expense.EntryDate,
            Memo = expense.Memo,
            Lines =
            [
                new JournalLineDraft
                {
                    AccountId = expense.ExpenseAccountId,
                    Debit = expense.Amount,
                    Memo = expense.Memo
                },
                new JournalLineDraft
                {
                    AccountId = expense.PaymentAccountId,
                    Credit = expense.Amount,
                    Memo = expense.Memo
                }
            ]
        }, cancellationToken);
    }

    public async Task<JournalEntry> PostIncomeAsync(
        ManualIncome income,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(income);
        ValidatePositiveAmount(income.Amount);

        var depositAccount = await GetRequiredAccountAsync(
            income.OrganizationId,
            income.DepositAccountId,
            cancellationToken);
        var incomeAccount = await GetRequiredAccountAsync(
            income.OrganizationId,
            income.IncomeAccountId,
            cancellationToken);

        EnsureActive(depositAccount);
        EnsureActive(incomeAccount);
        EnsureAccountType(depositAccount, AccountType.Asset, "Deposit account must be an Asset account.");
        EnsureAccountType(incomeAccount, AccountType.Income, "Income account must be an Income account.");

        return await PostJournalEntryAsync(new JournalEntryDraft
        {
            OrganizationId = income.OrganizationId,
            EntryDate = income.EntryDate,
            Memo = income.Memo,
            Lines =
            [
                new JournalLineDraft
                {
                    AccountId = income.DepositAccountId,
                    Debit = income.Amount,
                    Memo = income.Memo
                },
                new JournalLineDraft
                {
                    AccountId = income.IncomeAccountId,
                    Credit = income.Amount,
                    Memo = income.Memo
                }
            ]
        }, cancellationToken);
    }

    public async Task<JournalEntry> PostTransferAsync(
        ManualTransfer transfer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        ValidatePositiveAmount(transfer.Amount);

        if (transfer.FromAccountId == transfer.ToAccountId)
        {
            throw new AccountingException("Transfer accounts must be different.");
        }

        var fromAccount = await GetRequiredAccountAsync(
            transfer.OrganizationId,
            transfer.FromAccountId,
            cancellationToken);
        var toAccount = await GetRequiredAccountAsync(
            transfer.OrganizationId,
            transfer.ToAccountId,
            cancellationToken);

        EnsureActive(fromAccount);
        EnsureActive(toAccount);
        EnsureAnyAccountType(
            fromAccount,
            BalanceSheetAccountTypes,
            "Transfer source account must be an Asset, Liability, or Equity account.");
        EnsureAnyAccountType(
            toAccount,
            BalanceSheetAccountTypes,
            "Transfer destination account must be an Asset, Liability, or Equity account.");

        return await PostJournalEntryAsync(new JournalEntryDraft
        {
            OrganizationId = transfer.OrganizationId,
            EntryDate = transfer.EntryDate,
            Memo = transfer.Memo,
            Lines =
            [
                new JournalLineDraft
                {
                    AccountId = transfer.ToAccountId,
                    Debit = transfer.Amount,
                    Memo = transfer.Memo
                },
                new JournalLineDraft
                {
                    AccountId = transfer.FromAccountId,
                    Credit = transfer.Amount,
                    Memo = transfer.Memo
                }
            ]
        }, cancellationToken);
    }

    private async Task<Account> GetRequiredAccountAsync(
        Guid organizationId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        if (accountRepository is null)
        {
            throw new InvalidOperationException("Account repository is required for manual posting.");
        }

        return await accountRepository.GetByIdAsync(organizationId, accountId, cancellationToken)
            ?? throw new AccountingException("Account was not found for this organization.");
    }

    private static void ValidatePositiveAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new AccountingException("Amount must be greater than zero.");
        }
    }

    private static void EnsureActive(Account account)
    {
        if (!account.IsActive)
        {
            throw new AccountingException("Account must be active.");
        }
    }

    private static void EnsureAccountType(Account account, AccountType expectedType, string message)
    {
        if (account.AccountType != expectedType)
        {
            throw new AccountingException(message);
        }
    }

    private static void EnsureAnyAccountType(
        Account account,
        IReadOnlyCollection<AccountType> expectedTypes,
        string message)
    {
        if (!expectedTypes.Contains(account.AccountType))
        {
            throw new AccountingException(message);
        }
    }
}
