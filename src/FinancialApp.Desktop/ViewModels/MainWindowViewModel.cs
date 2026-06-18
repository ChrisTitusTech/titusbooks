using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinancialApp.Core.Api;
using FinancialApp.Core.Application;
using FinancialApp.Desktop.Services;

namespace FinancialApp.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private NavigationPage currentPage = NavigationPage.Home;

    [ObservableProperty]
    private string apiHealthMessage = "API health has not been checked.";

    [ObservableProperty]
    private string workspaceMessage = "Load or create an organization to begin.";

    [ObservableProperty]
    private string newOrganizationName = string.Empty;

    [ObservableProperty]
    private OrganizationSummary? selectedOrganization;

    [ObservableProperty]
    private string newAccountName = string.Empty;

    [ObservableProperty]
    private string selectedAccountType = "Expense";

    [ObservableProperty]
    private string accountSubtype = string.Empty;

    [ObservableProperty]
    private AccountSummary? selectedAccount;

    [ObservableProperty]
    private string selectedTransactionType = "Expense";

    [ObservableProperty]
    private string transactionDate = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [ObservableProperty]
    private string transactionAmount = string.Empty;

    [ObservableProperty]
    private string transactionMemo = string.Empty;

    [ObservableProperty]
    private AccountSummary? selectedFromAccount;

    [ObservableProperty]
    private AccountSummary? selectedToAccount;

    [ObservableProperty]
    private AccountSummary? selectedRegisterAccount;

    [ObservableProperty]
    private string selectedReport = "Profit and Loss";

    [ObservableProperty]
    private string reportStartDate = new DateOnly(DateTime.Today.Year, 1, 1)
        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [ObservableProperty]
    private string reportEndDate = DateOnly.FromDateTime(DateTime.Today)
        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [ObservableProperty]
    private string reportPrimaryLabel = "Total income";

    [ObservableProperty]
    private decimal reportPrimaryAmount;

    [ObservableProperty]
    private string reportSecondaryLabel = "Total expenses";

    [ObservableProperty]
    private decimal reportSecondaryAmount;

    [ObservableProperty]
    private string reportResultLabel = "Net income";

    [ObservableProperty]
    private decimal reportResultAmount;

    [ObservableProperty]
    private decimal dashboardIncome;

    [ObservableProperty]
    private decimal dashboardExpenses;

    [ObservableProperty]
    private decimal dashboardNetIncome;

    private readonly TitusBooksApiClient? apiClient;
    private readonly IReportFileSaver? reportFileSaver;
    private int accountLoadVersion;

    public MainWindowViewModel()
        : this(new AppSettings())
    {
    }

    public MainWindowViewModel(
        AppSettings settings,
        TitusBooksApiClient? apiClient = null,
        IReportFileSaver? reportFileSaver = null)
    {
        this.apiClient = apiClient;
        this.reportFileSaver = reportFileSaver;
        Title = settings.ApplicationName;
        DatabaseSummary = $"API target: {settings.Api.BaseUrl}";
    }

    public string Title { get; }

    public string DatabaseSummary { get; }

    public string PageTitle => CurrentPage switch
    {
        NavigationPage.Home => SelectedOrganization?.Name ?? "Home",
        NavigationPage.Accounts => "Chart of accounts",
        NavigationPage.Transactions => "Transactions",
        NavigationPage.Reports => "Reports",
        _ => "Company setup"
    };

    public string PageDescription => CurrentPage switch
    {
        NavigationPage.Home => "Your books at a glance.",
        NavigationPage.Accounts => "Create and maintain the accounts used by your books.",
        NavigationPage.Transactions => "Record money in, money out, and transfers between accounts.",
        NavigationPage.Reports => "Review financial performance for a selected date range.",
        _ => "Create a company and choose which set of books to work with."
    };

    public bool IsHomePage => CurrentPage == NavigationPage.Home;

    public bool IsSetupPage => CurrentPage == NavigationPage.Setup;

    public bool IsAccountsPage => CurrentPage == NavigationPage.Accounts;

    public bool IsTransactionsPage => CurrentPage == NavigationPage.Transactions;

    public bool IsReportsPage => CurrentPage == NavigationPage.Reports;

    public ObservableCollection<OrganizationSummary> Organizations { get; } = [];

    public ObservableCollection<AccountSummary> Accounts { get; } = [];

    public IReadOnlyList<string> AccountTypes { get; } =
    [
        "Asset",
        "Liability",
        "Equity",
        "Income",
        "Expense"
    ];

    public IReadOnlyList<string> TransactionTypes { get; } =
    [
        "Expense",
        "Income",
        "Transfer"
    ];

    public ObservableCollection<AccountSummary> FromAccountOptions { get; } = [];

    public ObservableCollection<AccountSummary> ToAccountOptions { get; } = [];

    public ObservableCollection<AccountRegisterEntrySummary> RegisterEntries { get; } = [];

    public ObservableCollection<ReportDisplayRow> ReportRows { get; } = [];

    public ObservableCollection<AccountReportTotalSummary> DashboardIncomeSources { get; } = [];

    public ObservableCollection<AccountReportTotalSummary> DashboardExpenseCategories { get; } = [];

    public string DashboardPeriod => $"{DateTime.Today:yyyy} year to date";

    public IReadOnlyList<string> ReportTypes { get; } =
    [
        "Profit and Loss",
        "Expenses by Category",
        "Income by Source"
    ];

    public string FromAccountLabel => SelectedTransactionType switch
    {
        "Income" => "Deposited to",
        "Transfer" => "From account",
        _ => "Paid from"
    };

    public string ToAccountLabel => SelectedTransactionType switch
    {
        "Income" => "Income source",
        "Transfer" => "To account",
        _ => "Expense category"
    };

    public string PostTransactionButtonText => SelectedTransactionType switch
    {
        "Income" => "Record income",
        "Transfer" => "Record transfer",
        _ => "Record expense"
    };

    public async Task CheckApiHealthAsync(ApiHealthClient apiHealthClient, CancellationToken cancellationToken = default)
    {
        var health = await apiHealthClient.CheckHealthAsync(cancellationToken);
        ApiHealthMessage = health.Message;
    }

    [RelayCommand]
    private async Task NavigateAsync(string page)
    {
        if (Enum.TryParse<NavigationPage>(page, ignoreCase: true, out var targetPage))
        {
            CurrentPage = targetPage;
            if (targetPage == NavigationPage.Home)
            {
                await LoadDashboardAsync();
            }
        }
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadOrganizationsAsync();
        await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        DashboardIncomeSources.Clear();
        DashboardExpenseCategories.Clear();
        DashboardIncome = 0;
        DashboardExpenses = 0;
        DashboardNetIncome = 0;

        if (apiClient is null || SelectedOrganization is null)
        {
            return;
        }

        try
        {
            var endDate = DateOnly.FromDateTime(DateTime.Today);
            var startDate = new DateOnly(endDate.Year, 1, 1);
            var report = await apiClient.GetProfitAndLossAsync(
                SelectedOrganization.Id,
                startDate,
                endDate);

            DashboardIncome = report.TotalIncome;
            DashboardExpenses = report.TotalExpenses;
            DashboardNetIncome = report.NetIncome;

            foreach (var account in report.Income.OrderByDescending(account => account.Amount).Take(5))
            {
                DashboardIncomeSources.Add(account);
            }

            foreach (var account in report.Expenses.OrderByDescending(account => account.Amount).Take(5))
            {
                DashboardExpenseCategories.Add(account);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task LoadOrganizationsAsync()
    {
        if (apiClient is null)
        {
            WorkspaceMessage = "API client is not configured.";
            return;
        }

        try
        {
            Organizations.Clear();
            var organizations = await apiClient.ListOrganizationsAsync();

            foreach (var organization in organizations)
            {
                Organizations.Add(organization);
            }

            SelectedOrganization ??= Organizations.FirstOrDefault();
            if (SelectedOrganization is null)
            {
                ClearAccountState();
            }

            WorkspaceMessage = Organizations.Count == 0
                ? "Create an organization to start bookkeeping."
                : "Organizations loaded.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task CreateOrganizationAsync()
    {
        if (apiClient is null)
        {
            WorkspaceMessage = "API client is not configured.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewOrganizationName))
        {
            WorkspaceMessage = "Organization name is required.";
            return;
        }

        try
        {
            var organization = await apiClient.CreateOrganizationAsync(new CreateOrganizationCommand(NewOrganizationName.Trim()));
            Organizations.Add(organization);
            SelectedOrganization = organization;
            NewOrganizationName = string.Empty;
            WorkspaceMessage = "Organization created.";
            await LoadAccountsAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task LoadAccountsAsync()
    {
        var organization = SelectedOrganization;
        if (organization is null)
        {
            ClearAccountState();
            return;
        }

        await LoadAccountsForOrganizationAsync(organization);
    }

    private async Task LoadAccountsForOrganizationAsync(OrganizationSummary organization)
    {
        if (apiClient is null)
        {
            WorkspaceMessage = "API client is not configured.";
            return;
        }

        var loadVersion = Interlocked.Increment(ref accountLoadVersion);

        try
        {
            var accounts = await apiClient.ListAccountsAsync(organization.Id);
            if (loadVersion != accountLoadVersion)
            {
                return;
            }

            ReplaceAccounts(accounts);
            SelectedAccount = Accounts.FirstOrDefault();
            SelectedRegisterAccount ??= Accounts.FirstOrDefault();
            RefreshTransactionAccountOptions();
            WorkspaceMessage = "Accounts loaded.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task SeedDefaultAccountsAsync()
    {
        if (apiClient is null || SelectedOrganization is null)
        {
            WorkspaceMessage = "Select an organization first.";
            return;
        }

        try
        {
            var result = await apiClient.SeedDefaultAccountsAsync(SelectedOrganization.Id);
            WorkspaceMessage = result.CreatedCount == 0
                ? "Default accounts were already present."
                : $"Created {result.CreatedCount} default accounts.";
            await LoadAccountsAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task CreateAccountAsync()
    {
        if (apiClient is null || SelectedOrganization is null)
        {
            WorkspaceMessage = "Select an organization first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewAccountName))
        {
            WorkspaceMessage = "Account name is required.";
            return;
        }

        try
        {
            var account = await apiClient.CreateAccountAsync(
                SelectedOrganization.Id,
                new CreateAccountCommand(
                    NewAccountName.Trim(),
                    SelectedAccountType,
                    string.IsNullOrWhiteSpace(AccountSubtype) ? null : AccountSubtype.Trim()));
            Accounts.Add(account);
            SelectedAccount = account;
            SelectedRegisterAccount ??= account;
            RefreshTransactionAccountOptions();
            NewAccountName = string.Empty;
            AccountSubtype = string.Empty;
            WorkspaceMessage = "Account created.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task RenameSelectedAccountAsync()
    {
        if (apiClient is null || SelectedOrganization is null || SelectedAccount is null)
        {
            WorkspaceMessage = "Select an account first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewAccountName))
        {
            WorkspaceMessage = "Enter the new account name.";
            return;
        }

        try
        {
            var updatedAccount = await apiClient.UpdateAccountAsync(
                SelectedOrganization.Id,
                SelectedAccount.Id,
                new UpdateAccountCommand(
                    NewAccountName.Trim(),
                    string.IsNullOrWhiteSpace(AccountSubtype) ? SelectedAccount.AccountSubtype : AccountSubtype.Trim()));

            ReplaceAccount(updatedAccount);
            SelectedAccount = updatedAccount;
            NewAccountName = string.Empty;
            AccountSubtype = string.Empty;
            WorkspaceMessage = "Account updated.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task DeactivateSelectedAccountAsync()
    {
        if (apiClient is null || SelectedOrganization is null || SelectedAccount is null)
        {
            WorkspaceMessage = "Select an account first.";
            return;
        }

        try
        {
            var deactivatedAccount = await apiClient.DeactivateAccountAsync(SelectedOrganization.Id, SelectedAccount.Id);
            ReplaceAccount(deactivatedAccount);
            SelectedAccount = deactivatedAccount;
            WorkspaceMessage = "Account deactivated.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task PostManualTransactionAsync()
    {
        if (apiClient is null || SelectedOrganization is null)
        {
            WorkspaceMessage = "Select an organization first.";
            return;
        }

        if (SelectedFromAccount is null || SelectedToAccount is null)
        {
            WorkspaceMessage = "Select both accounts for the transaction.";
            return;
        }

        if (!DateOnly.TryParse(TransactionDate, CultureInfo.InvariantCulture, out var entryDate))
        {
            WorkspaceMessage = "Enter the date as YYYY-MM-DD.";
            return;
        }

        if (!decimal.TryParse(
            TransactionAmount,
            NumberStyles.Currency,
            CultureInfo.InvariantCulture,
            out var amount)
            || amount <= 0)
        {
            WorkspaceMessage = "Enter an amount greater than zero.";
            return;
        }

        try
        {
            var memo = string.IsNullOrWhiteSpace(TransactionMemo) ? null : TransactionMemo.Trim();
            var entry = SelectedTransactionType switch
            {
                "Income" => await apiClient.PostIncomeAsync(
                    SelectedOrganization.Id,
                    new PostIncomeCommand(entryDate, SelectedFromAccount.Id, SelectedToAccount.Id, amount, memo)),
                "Transfer" => await apiClient.PostTransferAsync(
                    SelectedOrganization.Id,
                    new PostTransferCommand(entryDate, SelectedFromAccount.Id, SelectedToAccount.Id, amount, memo)),
                _ => await apiClient.PostExpenseAsync(
                    SelectedOrganization.Id,
                    new PostExpenseCommand(entryDate, SelectedFromAccount.Id, SelectedToAccount.Id, amount, memo))
            };

            TransactionAmount = string.Empty;
            TransactionMemo = string.Empty;
            WorkspaceMessage = $"Transaction posted. Journal entry {entry.Id}.";
            await LoadRegisterAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task LoadRegisterAsync()
    {
        if (apiClient is null || SelectedOrganization is null || SelectedRegisterAccount is null)
        {
            RegisterEntries.Clear();
            return;
        }

        try
        {
            RegisterEntries.Clear();
            var entries = await apiClient.ListRegisterAsync(SelectedOrganization.Id, SelectedRegisterAccount.Id);

            foreach (var entry in entries)
            {
                RegisterEntries.Add(entry);
            }

            WorkspaceMessage = RegisterEntries.Count == 0
                ? "No register entries for the selected account."
                : "Register loaded.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task LoadReportAsync()
    {
        if (!TryGetReportRequest(out var organizationId, out var startDate, out var endDate))
        {
            return;
        }

        try
        {
            ReportRows.Clear();

            switch (SelectedReport)
            {
                case "Expenses by Category":
                    DisplayBreakdown(
                        await apiClient!.GetExpensesByCategoryAsync(organizationId, startDate, endDate),
                        "Expenses");
                    break;
                case "Income by Source":
                    DisplayBreakdown(
                        await apiClient!.GetIncomeBySourceAsync(organizationId, startDate, endDate),
                        "Income");
                    break;
                default:
                    DisplayProfitAndLoss(
                        await apiClient!.GetProfitAndLossAsync(organizationId, startDate, endDate));
                    break;
            }

            WorkspaceMessage = ReportRows.Count == 0
                ? "The selected report has no activity for this date range."
                : $"{SelectedReport} loaded.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task ExportReportAsync()
    {
        if (!TryGetReportRequest(out var organizationId, out var startDate, out var endDate))
        {
            return;
        }

        if (reportFileSaver is null)
        {
            WorkspaceMessage = "Report file export is not available.";
            return;
        }

        try
        {
            var reportName = SelectedReport switch
            {
                "Expenses by Category" => "expenses-by-category",
                "Income by Source" => "income-by-source",
                _ => "profit-and-loss"
            };
            var csv = await apiClient!.GetReportCsvAsync(organizationId, reportName, startDate, endDate);
            var saved = await reportFileSaver.SaveAsync(
                $"{reportName}-{startDate:yyyy-MM-dd}-to-{endDate:yyyy-MM-dd}.csv",
                csv);

            WorkspaceMessage = saved ? "Report exported." : "Report export canceled.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or IOException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    partial void OnSelectedOrganizationChanged(OrganizationSummary? value)
    {
        OnPropertyChanged(nameof(PageTitle));

        if (value is null)
        {
            ClearAccountState();
            return;
        }

        _ = LoadAccountsForOrganizationAsync(value);
        _ = LoadDashboardAsync();
    }

    partial void OnCurrentPageChanged(NavigationPage value)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageDescription));
        OnPropertyChanged(nameof(IsHomePage));
        OnPropertyChanged(nameof(IsSetupPage));
        OnPropertyChanged(nameof(IsAccountsPage));
        OnPropertyChanged(nameof(IsTransactionsPage));
        OnPropertyChanged(nameof(IsReportsPage));
    }

    partial void OnSelectedTransactionTypeChanged(string value)
    {
        OnPropertyChanged(nameof(FromAccountLabel));
        OnPropertyChanged(nameof(ToAccountLabel));
        OnPropertyChanged(nameof(PostTransactionButtonText));
        RefreshTransactionAccountOptions();
    }

    partial void OnSelectedRegisterAccountChanged(AccountSummary? value)
    {
        _ = LoadRegisterAsync();
    }

    private void ReplaceAccount(AccountSummary account)
    {
        var existingIndex = Accounts
            .Select((existingAccount, index) => new { existingAccount, index })
            .FirstOrDefault(item => item.existingAccount.Id == account.Id)
            ?.index;

        if (existingIndex is null)
        {
            Accounts.Add(account);
            RefreshTransactionAccountOptions();
            return;
        }

        Accounts[existingIndex.Value] = account;
        RefreshTransactionAccountOptions();
    }

    private void ReplaceAccounts(IEnumerable<AccountSummary> accounts)
    {
        Accounts.Clear();
        foreach (var account in accounts)
        {
            Accounts.Add(account);
        }
    }

    private void ClearAccountState()
    {
        Accounts.Clear();
        RegisterEntries.Clear();
        SelectedAccount = null;
        SelectedFromAccount = null;
        SelectedToAccount = null;
        SelectedRegisterAccount = null;
        ClearReport();
        RefreshTransactionAccountOptions();
    }

    private bool TryGetReportRequest(
        out Guid organizationId,
        out DateOnly startDate,
        out DateOnly endDate)
    {
        organizationId = SelectedOrganization?.Id ?? Guid.Empty;
        startDate = default;
        endDate = default;

        if (apiClient is null || SelectedOrganization is null)
        {
            WorkspaceMessage = "Select an organization first.";
            return false;
        }

        if (!DateOnly.TryParse(ReportStartDate, CultureInfo.InvariantCulture, out startDate)
            || !DateOnly.TryParse(ReportEndDate, CultureInfo.InvariantCulture, out endDate))
        {
            WorkspaceMessage = "Enter report dates as YYYY-MM-DD.";
            return false;
        }

        if (startDate > endDate)
        {
            WorkspaceMessage = "Report start date must be on or before end date.";
            return false;
        }

        return true;
    }

    private void DisplayProfitAndLoss(ProfitAndLossReportSummary report)
    {
        foreach (var account in report.Income)
        {
            ReportRows.Add(new ReportDisplayRow("Income", account.AccountName, account.Amount));
        }

        foreach (var account in report.Expenses)
        {
            ReportRows.Add(new ReportDisplayRow("Expense", account.AccountName, account.Amount));
        }

        ReportPrimaryLabel = "Total income";
        ReportPrimaryAmount = report.TotalIncome;
        ReportSecondaryLabel = "Total expenses";
        ReportSecondaryAmount = report.TotalExpenses;
        ReportResultLabel = "Net income";
        ReportResultAmount = report.NetIncome;
    }

    private void DisplayBreakdown(
        AccountBreakdownReportSummary report,
        string section)
    {
        foreach (var account in report.Accounts)
        {
            ReportRows.Add(new ReportDisplayRow(section, account.AccountName, account.Amount));
        }

        var largestAccount = report.Accounts.MaxBy(account => account.Amount);
        ReportPrimaryLabel = largestAccount is null ? "Largest account" : $"Largest: {largestAccount.AccountName}";
        ReportPrimaryAmount = largestAccount?.Amount ?? 0;
        ReportSecondaryLabel = report.Accounts.Count == 0 ? "Average per account" : $"{report.Accounts.Count} accounts";
        ReportSecondaryAmount = report.Accounts.Count == 0 ? 0 : report.Total / report.Accounts.Count;
        ReportResultLabel = "Report total";
        ReportResultAmount = report.Total;
    }

    private void ClearReport()
    {
        ReportRows.Clear();
        ReportPrimaryAmount = 0;
        ReportSecondaryAmount = 0;
        ReportResultAmount = 0;
    }

    private void RefreshTransactionAccountOptions()
    {
        FromAccountOptions.Clear();
        ToAccountOptions.Clear();

        var fromAccounts = Accounts.Where(IsFromAccountOption).ToList();
        var toAccounts = Accounts.Where(IsToAccountOption).ToList();

        foreach (var account in fromAccounts)
        {
            FromAccountOptions.Add(account);
        }

        foreach (var account in toAccounts)
        {
            ToAccountOptions.Add(account);
        }

        if (SelectedFromAccount is null || !fromAccounts.Any(account => account.Id == SelectedFromAccount.Id))
        {
            SelectedFromAccount = fromAccounts.FirstOrDefault();
        }

        if (SelectedToAccount is null || !toAccounts.Any(account => account.Id == SelectedToAccount.Id))
        {
            SelectedToAccount = toAccounts.FirstOrDefault();
        }
    }

    private bool IsFromAccountOption(AccountSummary account)
    {
        return account.IsActive && SelectedTransactionType switch
        {
            "Income" => account.AccountType == "Asset",
            "Transfer" => IsBalanceSheetAccount(account),
            _ => account.AccountType is "Asset" or "Liability"
        };
    }

    private bool IsToAccountOption(AccountSummary account)
    {
        return account.IsActive && SelectedTransactionType switch
        {
            "Income" => account.AccountType == "Income",
            "Transfer" => IsBalanceSheetAccount(account),
            _ => account.AccountType == "Expense"
        };
    }

    private static bool IsBalanceSheetAccount(AccountSummary account)
    {
        return account.AccountType is "Asset" or "Liability" or "Equity";
    }
}
