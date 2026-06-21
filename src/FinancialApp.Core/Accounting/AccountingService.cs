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

    public AccountingService(IJournalEntryRepository journalEntryRepository)
    {
        this.journalEntryRepository = journalEntryRepository;
    }

    public AccountingService(IJournalEntryRepository journalEntryRepository, IAccountRepository accountRepository)
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

        return await PostTwoLineEntryAsync(
            expense.OrganizationId,
            expense.EntryDate,
            expense.Memo,
            DebitLine(expense.ExpenseAccountId, expense.Amount, expense.Memo),
            CreditLine(expense.PaymentAccountId, expense.Amount, expense.Memo),
            cancellationToken);
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

        return await PostTwoLineEntryAsync(
            income.OrganizationId,
            income.EntryDate,
            income.Memo,
            DebitLine(income.DepositAccountId, income.Amount, income.Memo),
            CreditLine(income.IncomeAccountId, income.Amount, income.Memo),
            cancellationToken);
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

        return await PostTwoLineEntryAsync(
            transfer.OrganizationId,
            transfer.EntryDate,
            transfer.Memo,
            DebitLine(transfer.ToAccountId, transfer.Amount, transfer.Memo),
            CreditLine(transfer.FromAccountId, transfer.Amount, transfer.Memo),
            cancellationToken);
    }

    public async Task<JournalEntry> PostPayPalSaleAsync(
        PayPalSale sale,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);
        ValidatePositiveAmount(sale.GrossAmount);
        ValidatePositiveAmount(sale.NetAmount);
        if (sale.FeeAmount < 0)
        {
            throw new AccountingException("PayPal fee amount cannot be negative.");
        }

        if (sale.GrossAmount != sale.NetAmount + sale.FeeAmount)
        {
            throw new AccountingException("PayPal gross amount must equal net amount plus fee.");
        }

        var payPalAccount = await GetRequiredAccountAsync(
            sale.OrganizationId,
            sale.PayPalAccountId,
            cancellationToken);
        var incomeAccount = await GetRequiredAccountAsync(
            sale.OrganizationId,
            sale.IncomeAccountId,
            cancellationToken);
        var feeAccount = await GetRequiredAccountAsync(
            sale.OrganizationId,
            sale.MerchantFeeAccountId,
            cancellationToken);

        EnsureActive(payPalAccount);
        EnsureActive(incomeAccount);
        EnsureActive(feeAccount);
        EnsureAccountType(payPalAccount, AccountType.Asset, "PayPal account must be an Asset account.");
        EnsureAccountType(incomeAccount, AccountType.Income, "PayPal income account must be an Income account.");
        EnsureAccountType(feeAccount, AccountType.Expense, "PayPal fee account must be an Expense account.");

        var lines = new List<JournalLineDraft>
        {
            DebitLine(sale.PayPalAccountId, sale.NetAmount, sale.Memo),
            CreditLine(sale.IncomeAccountId, sale.GrossAmount, sale.Memo)
        };
        if (sale.FeeAmount > 0)
        {
            lines.Insert(1, DebitLine(sale.MerchantFeeAccountId, sale.FeeAmount, sale.Memo));
        }

        return await PostJournalEntryAsync(new JournalEntryDraft
        {
            OrganizationId = sale.OrganizationId,
            EntryDate = sale.EntryDate,
            Memo = NormalizeMemo(sale.Memo),
            SourceImportedTransactionId = sale.SourceImportedTransactionId,
            Lines = lines
        }, cancellationToken);
    }

    private async Task<JournalEntry> PostTwoLineEntryAsync(
        Guid organizationId,
        DateOnly entryDate,
        string? memo,
        JournalLineDraft debitLine,
        JournalLineDraft creditLine,
        CancellationToken cancellationToken)
    {
        return await PostJournalEntryAsync(new JournalEntryDraft
        {
            OrganizationId = organizationId,
            EntryDate = entryDate,
            Memo = NormalizeMemo(memo),
            Lines = [debitLine, creditLine]
        }, cancellationToken);
    }

    private static JournalLineDraft DebitLine(Guid accountId, decimal amount, string? memo)
    {
        return new JournalLineDraft
        {
            AccountId = accountId,
            Debit = amount,
            Memo = NormalizeMemo(memo)
        };
    }

    private static JournalLineDraft CreditLine(Guid accountId, decimal amount, string? memo)
    {
        return new JournalLineDraft
        {
            AccountId = accountId,
            Credit = amount,
            Memo = NormalizeMemo(memo)
        };
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

    private static string? NormalizeMemo(string? memo)
    {
        return string.IsNullOrWhiteSpace(memo) ? null : memo.Trim();
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
