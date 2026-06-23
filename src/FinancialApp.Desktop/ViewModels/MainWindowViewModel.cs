using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinancialApp.Core.Api;
using FinancialApp.Core.Application;
using FinancialApp.Desktop.Services;
using FinancialApp.Importers;

namespace FinancialApp.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private const string UnmappedColumn = "(Not mapped)";

    [ObservableProperty]
    private NavigationPage currentPage = NavigationPage.Home;

    [ObservableProperty]
    private string apiHealthMessage = "API health has not been checked.";

    [ObservableProperty]
    private string workspaceMessage = "Load or create an organization to begin.";

    [ObservableProperty]
    private string apiBaseUrl = string.Empty;

    [ObservableProperty]
    private bool allowInsecureRemoteHttp;

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

    [ObservableProperty]
    private string importSource = "Generic CSV";

    [ObservableProperty]
    private string? importFileName;

    [ObservableProperty]
    private string? selectedDateColumn;

    [ObservableProperty]
    private string? selectedDescriptionColumn;

    [ObservableProperty]
    private string? selectedAmountColumn;

    [ObservableProperty]
    private string? selectedDebitColumn;

    [ObservableProperty]
    private string? selectedCreditColumn;

    [ObservableProperty]
    private string? selectedSourceIdColumn;

    [ObservableProperty]
    private string? selectedCurrencyColumn;

    [ObservableProperty]
    private string? selectedBalanceColumn;

    [ObservableProperty]
    private string defaultImportCurrency = "USD";

    [ObservableProperty]
    private int importValidCount;

    [ObservableProperty]
    private int importErrorCount;

    [ObservableProperty]
    private int importSkippedCount;

    [ObservableProperty]
    private string selectedImportStatusFilter = "Pending";

    [ObservableProperty]
    private AccountSummary? selectedImportCategory;

    [ObservableProperty]
    private AccountSummary? selectedImportPostingAccount;

    [ObservableProperty]
    private AccountSummary? selectedImportFeeAccount;

    [ObservableProperty]
    private AccountSummary? selectedReconciliationAccount;

    [ObservableProperty]
    private string reconciliationStatementEndDate = DateOnly.FromDateTime(DateTime.Today)
        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [ObservableProperty]
    private string reconciliationStatementEndBalance = string.Empty;

    [ObservableProperty]
    private decimal reconciliationClearedBalance;

    [ObservableProperty]
    private decimal reconciliationDifference;

    private readonly TitusBooksApiClient? apiClient;
    private readonly IReportFileSaver? reportFileSaver;
    private readonly IImportFilePicker? importFilePicker;
    private readonly UserSettingsStore? userSettingsStore;
    private readonly int apiRequestTimeoutSeconds;
    private string? importCsvContent;
    private bool skipBalanceOnlyImportRows;
    private int accountLoadVersion;
    private int dashboardLoadVersion;
    private int importLoadVersion;
    private int reconciliationLoadVersion;

    public MainWindowViewModel()
        : this(new AppSettings())
    {
    }

    public MainWindowViewModel(
        AppSettings settings,
        TitusBooksApiClient? apiClient = null,
        IReportFileSaver? reportFileSaver = null,
        IImportFilePicker? importFilePicker = null,
        UserSettingsStore? userSettingsStore = null)
    {
        this.apiClient = apiClient;
        this.reportFileSaver = reportFileSaver;
        this.importFilePicker = importFilePicker;
        this.userSettingsStore = userSettingsStore;
        apiRequestTimeoutSeconds = settings.Api.RequestTimeoutSeconds;
        Title = settings.ApplicationName;
        ApiBaseUrl = settings.Api.BaseUrl;
        AllowInsecureRemoteHttp = settings.Api.AllowInsecureRemoteHttp;
        ApiEndpointSummary = $"API target: {settings.Api.BaseUrl}";
    }

    public string Title { get; }

    public string ApiEndpointSummary { get; }

    public string ApiTransportMessage =>
        AllowInsecureRemoteHttp
            ? "Remote HTTP is enabled. Use this only on a trusted development network."
            : "HTTPS is required when the API is not running on this computer.";

    public string PageTitle => CurrentPage switch
    {
        NavigationPage.Home => SelectedOrganization?.Name ?? "Home",
        NavigationPage.Accounts => "Chart of accounts",
        NavigationPage.Transactions => "Transactions",
        NavigationPage.Imports => "Import transactions",
        NavigationPage.Reports => "Reports",
        NavigationPage.Reconciliation => "Reconciliation",
        _ => "Company setup"
    };

    public string PageDescription => CurrentPage switch
    {
        NavigationPage.Home => "Your books at a glance.",
        NavigationPage.Accounts => "Create and maintain the accounts used by your books.",
        NavigationPage.Transactions => "Record money in, money out, and transfers between accounts.",
        NavigationPage.Imports => "Map a CSV file and review transactions before they enter staging.",
        NavigationPage.Reports => "Review financial performance for a selected date range.",
        NavigationPage.Reconciliation => "Match account activity to an external statement.",
        _ => "Create a company and choose which set of books to work with."
    };

    public bool IsHomePage => CurrentPage == NavigationPage.Home;

    public bool IsSetupPage => CurrentPage == NavigationPage.Setup;

    public bool IsAccountsPage => CurrentPage == NavigationPage.Accounts;

    public bool IsTransactionsPage => CurrentPage == NavigationPage.Transactions;

    public bool IsImportsPage => CurrentPage == NavigationPage.Imports;

    public bool IsReportsPage => CurrentPage == NavigationPage.Reports;

    public bool IsReconciliationPage => CurrentPage == NavigationPage.Reconciliation;

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

    public ObservableCollection<AccountSummary> ReconciliationAccountOptions { get; } = [];

    public ObservableCollection<AccountRegisterEntrySummary> RegisterEntries { get; } = [];

    public ObservableCollection<ReportDisplayRow> ReportRows { get; } = [];

    public ObservableCollection<string> CsvHeaders { get; } = [];

    public IReadOnlyList<string> ImportProfiles { get; } =
        CsvImportProfiles.All.Select(profile => profile.Name).ToList();

    public ObservableCollection<CsvImportPreviewRowSummary> ImportPreviewRows { get; } = [];

    public ObservableCollection<ImportInboxItemViewModel> ImportedTransactions { get; } = [];

    public ObservableCollection<AccountSummary> ImportPostingAccountOptions { get; } = [];

    public ObservableCollection<AccountSummary> ImportFeeAccountOptions { get; } = [];

    public ObservableCollection<ReconciliationTransactionItemViewModel> ReconciliationTransactions { get; } = [];

    public IReadOnlyList<string> ImportStatusFilters { get; } =
    [
        "All",
        "Pending",
        "Categorized",
        "Duplicate"
    ];

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
            else if (targetPage == NavigationPage.Imports)
            {
                await LoadImportedTransactionsAsync();
            }
            else if (targetPage == NavigationPage.Reconciliation)
            {
                await LoadReconciliationAsync();
            }
        }
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadOrganizationsAsync();
        await LoadDashboardAsync();
    }

    [RelayCommand]
    private async Task SaveApiSettingsAsync()
    {
        if (userSettingsStore is null)
        {
            WorkspaceMessage = "User settings storage is not available.";
            return;
        }

        try
        {
            var endpoint = ApiEndpointValidator.Validate(ApiBaseUrl, AllowInsecureRemoteHttp);
            var settings = new ApiSettings
            {
                BaseUrl = endpoint.AbsoluteUri.TrimEnd('/'),
                RequestTimeoutSeconds = apiRequestTimeoutSeconds,
                AllowInsecureRemoteHttp = AllowInsecureRemoteHttp,
            };

            await userSettingsStore.SaveApiSettingsAsync(settings);
            ApiBaseUrl = settings.BaseUrl;
            WorkspaceMessage = "API settings saved. Restart TitusBooks to use the new endpoint.";
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or IOException
            or UnauthorizedAccessException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    private async Task LoadDashboardAsync()
    {
        var organization = SelectedOrganization;
        var loadVersion = Interlocked.Increment(ref dashboardLoadVersion);
        ClearDashboard();
        if (apiClient is null || organization is null)
        {
            return;
        }

        try
        {
            var endDate = DateOnly.FromDateTime(DateTime.Today);
            var startDate = new DateOnly(endDate.Year, 1, 1);
            var report = await apiClient.GetProfitAndLossAsync(
                organization.Id,
                startDate,
                endDate);
            if (loadVersion != dashboardLoadVersion)
            {
                return;
            }

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
            SelectedImportCategory = Accounts.FirstOrDefault(account =>
                account.IsActive
                && account.AccountType is "Expense" or "Income");
            RefreshImportPostingAccountOptions();
            RefreshImportFeeAccountOptions();
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
            CultureInfo.CurrentCulture,
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

    [RelayCommand]
    public async Task ChooseImportFileAsync()
    {
        if (apiClient is null || importFilePicker is null)
        {
            WorkspaceMessage = "CSV file selection is not available.";
            return;
        }

        try
        {
            var file = await importFilePicker.PickCsvAsync();
            if (file is null)
            {
                WorkspaceMessage = "CSV selection canceled.";
                return;
            }

            var headers = await apiClient.ReadCsvHeadersAsync(file.Content);
            importCsvContent = file.Content;
            ImportFileName = file.FileName;
            ReplaceCsvHeaders(headers);
            ApplySuggestedMappings(headers);
            ImportPreviewRows.Clear();
            ImportValidCount = 0;
            ImportErrorCount = 0;
            ImportSkippedCount = 0;
            WorkspaceMessage = $"Loaded {file.FileName}. Map the columns, then preview.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or IOException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task PreviewImportAsync()
    {
        if (!TryCreateImportCommand(out var organizationId, out var command))
        {
            return;
        }

        try
        {
            var preview = await apiClient!.PreviewCsvImportAsync(organizationId, command);
            ImportPreviewRows.Clear();
            foreach (var row in preview.Rows)
            {
                ImportPreviewRows.Add(row);
            }

            ImportValidCount = preview.ValidCount;
            ImportErrorCount = preview.ErrorCount;
            ImportSkippedCount = preview.SkippedCount;
            WorkspaceMessage =
                $"Preview ready: {preview.ValidCount} valid, {preview.SkippedCount} skipped, {preview.ErrorCount} errors.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task ImportCsvAsync()
    {
        if (!TryCreateImportCommand(out var organizationId, out var command))
        {
            return;
        }

        try
        {
            var result = await apiClient!.ImportCsvAsync(organizationId, command);
            WorkspaceMessage =
                $"Import complete: {result.PendingCount} pending, {result.CategorizedCount} categorized, {result.DuplicateCount} duplicates, {result.SkippedCount} skipped, {result.ErrorCount} errors.";
            await LoadImportedTransactionsAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task LoadImportedTransactionsAsync()
    {
        var organization = SelectedOrganization;
        var loadVersion = Interlocked.Increment(ref importLoadVersion);
        ImportedTransactions.Clear();
        if (apiClient is null || organization is null)
        {
            return;
        }

        try
        {
            var status = string.Equals(
                SelectedImportStatusFilter,
                "All",
                StringComparison.OrdinalIgnoreCase)
                ? null
                : SelectedImportStatusFilter;
            var transactions = await apiClient.ListImportedTransactionsAsync(
                organization.Id,
                status);
            if (loadVersion != importLoadVersion)
            {
                return;
            }

            foreach (var transaction in transactions)
            {
                ImportedTransactions.Add(new ImportInboxItemViewModel(transaction));
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task CategorizeSelectedImportsAsync()
    {
        if (apiClient is null || SelectedOrganization is null)
        {
            WorkspaceMessage = "Select an organization first.";
            return;
        }

        if (SelectedImportCategory is null)
        {
            WorkspaceMessage = "Select a category first.";
            return;
        }

        var selectedIds = ImportedTransactions
            .Where(transaction => transaction.IsSelected)
            .Select(transaction => transaction.Id)
            .ToList();
        if (selectedIds.Count == 0)
        {
            WorkspaceMessage = "Select at least one imported transaction.";
            return;
        }

        try
        {
            await apiClient.CategorizeImportedTransactionsAsync(
                SelectedOrganization.Id,
                new CategorizeImportedTransactionsCommand(
                    selectedIds,
                    SelectedImportCategory.Id));
            WorkspaceMessage =
                $"{selectedIds.Count} imported transaction(s) categorized as {SelectedImportCategory.Name}.";
            await LoadImportedTransactionsAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task CreateRuleFromSelectedImportAsync()
    {
        if (apiClient is null || SelectedOrganization is null)
        {
            WorkspaceMessage = "Select an organization first.";
            return;
        }

        if (SelectedImportCategory is null)
        {
            WorkspaceMessage = "Select a category for the rule.";
            return;
        }

        var selected = ImportedTransactions
            .Where(transaction => transaction.IsSelected)
            .ToList();
        if (selected.Count != 1)
        {
            WorkspaceMessage = "Select exactly one imported transaction to create a rule.";
            return;
        }

        try
        {
            var transaction = selected[0];
            var rule = await apiClient.CreateCategorizationRuleAsync(
                SelectedOrganization.Id,
                new CreateCategorizationRuleCommand(
                    $"Match {transaction.Description}",
                    "description",
                    "contains",
                    transaction.Description,
                    SelectedImportCategory.Id));
            WorkspaceMessage = $"Rule '{rule.Name}' created for future imports.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task PostSelectedImportsAsync()
    {
        if (apiClient is null || SelectedOrganization is null)
        {
            WorkspaceMessage = "Select an organization first.";
            return;
        }

        if (SelectedImportPostingAccount is null)
        {
            WorkspaceMessage = "Select the account where these transactions occurred.";
            return;
        }

        var selectedIds = ImportedTransactions
            .Where(transaction => transaction.IsSelected)
            .Select(transaction => transaction.Id)
            .ToList();
        if (selectedIds.Count == 0)
        {
            WorkspaceMessage = "Select at least one categorized transaction to post.";
            return;
        }

        try
        {
            var result = await apiClient.PostImportedTransactionsAsync(
                SelectedOrganization.Id,
                new PostImportedTransactionsCommand(
                    selectedIds,
                    SelectedImportPostingAccount.Id,
                    SelectedImportFeeAccount?.Id));
            WorkspaceMessage = $"{result.PostedCount} imported transaction(s) posted.";
            await LoadImportedTransactionsAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            WorkspaceMessage = exception.Message;
        }
    }

    [RelayCommand]
    public async Task LoadReconciliationAsync()
    {
        var loadVersion = Interlocked.Increment(ref reconciliationLoadVersion);
        if (!TryCreateReconciliationCommand(out var organizationId, out var accountId, out var command))
        {
            ReconciliationTransactions.Clear();
            ReconciliationClearedBalance = 0;
            ReconciliationDifference = 0;
            return;
        }

        try
        {
            var preview = await apiClient!.PreviewReconciliationAsync(
                organizationId,
                accountId,
                command);
            if (loadVersion != reconciliationLoadVersion)
            {
                return;
            }

            DisplayReconciliation(preview);
            WorkspaceMessage = preview.Difference == 0
                ? "Reconciliation is balanced and ready to complete."
                : $"Reconciliation difference: {preview.Difference:C}.";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            if (loadVersion == reconciliationLoadVersion)
            {
                WorkspaceMessage = exception.Message;
            }
        }
    }

    [RelayCommand]
    public async Task CompleteReconciliationAsync()
    {
        if (!TryCreateReconciliationCommand(out var organizationId, out var accountId, out var command))
        {
            return;
        }

        try
        {
            var completed = await apiClient!.CompleteReconciliationAsync(
                organizationId,
                accountId,
                command);
            DisplayReconciliation(completed);
            WorkspaceMessage = "Reconciliation completed.";
            await LoadReconciliationAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
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
        if (CurrentPage == NavigationPage.Home)
        {
            _ = LoadDashboardAsync();
        }
        else if (CurrentPage == NavigationPage.Imports)
        {
            _ = LoadImportedTransactionsAsync();
        }
        else if (CurrentPage == NavigationPage.Reconciliation)
        {
            _ = LoadReconciliationAsync();
        }
    }

    partial void OnCurrentPageChanged(NavigationPage value)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageDescription));
        OnPropertyChanged(nameof(IsHomePage));
        OnPropertyChanged(nameof(IsSetupPage));
        OnPropertyChanged(nameof(IsAccountsPage));
        OnPropertyChanged(nameof(IsTransactionsPage));
        OnPropertyChanged(nameof(IsImportsPage));
        OnPropertyChanged(nameof(IsReportsPage));
        OnPropertyChanged(nameof(IsReconciliationPage));
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

    partial void OnSelectedImportStatusFilterChanged(string value)
    {
        if (CurrentPage == NavigationPage.Imports)
        {
            _ = LoadImportedTransactionsAsync();
        }
    }

    partial void OnSelectedReconciliationAccountChanged(AccountSummary? value)
    {
        if (CurrentPage == NavigationPage.Reconciliation)
        {
            _ = LoadReconciliationAsync();
        }
    }

    partial void OnImportSourceChanged(string value)
    {
        if (CsvHeaders.Count > 1)
        {
            try
            {
                ApplySuggestedMappings(CsvHeaders.Skip(1).ToList());
                WorkspaceMessage = $"{value} profile applied.";
            }
            catch (InvalidOperationException exception)
            {
                WorkspaceMessage = exception.Message;
            }
        }
    }

    partial void OnAllowInsecureRemoteHttpChanged(bool value)
    {
        OnPropertyChanged(nameof(ApiTransportMessage));
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
        SelectedImportCategory = null;
        SelectedImportPostingAccount = null;
        SelectedImportFeeAccount = null;
        SelectedReconciliationAccount = null;
        ImportedTransactions.Clear();
        ImportPostingAccountOptions.Clear();
        ImportFeeAccountOptions.Clear();
        ReconciliationAccountOptions.Clear();
        ReconciliationTransactions.Clear();
        Interlocked.Increment(ref dashboardLoadVersion);
        Interlocked.Increment(ref importLoadVersion);
        Interlocked.Increment(ref reconciliationLoadVersion);
        ClearDashboard();
        ClearReport();
        RefreshTransactionAccountOptions();
    }

    private bool TryCreateReconciliationCommand(
        out Guid organizationId,
        out Guid accountId,
        out ReconciliationCommand command)
    {
        organizationId = SelectedOrganization?.Id ?? Guid.Empty;
        accountId = SelectedReconciliationAccount?.Id ?? Guid.Empty;
        command = null!;

        if (apiClient is null || SelectedOrganization is null)
        {
            WorkspaceMessage = "Select an organization first.";
            return false;
        }

        if (SelectedReconciliationAccount is null)
        {
            WorkspaceMessage = "Select an account to reconcile.";
            return false;
        }

        if (!DateOnly.TryParse(
            ReconciliationStatementEndDate,
            CultureInfo.InvariantCulture,
            out var statementEndDate))
        {
            WorkspaceMessage = "Enter the statement ending date as YYYY-MM-DD.";
            return false;
        }

        if (!decimal.TryParse(
            ReconciliationStatementEndBalance,
            NumberStyles.Currency,
            CultureInfo.CurrentCulture,
            out var statementEndBalance))
        {
            WorkspaceMessage = "Enter a valid statement ending balance.";
            return false;
        }

        command = new ReconciliationCommand(
            statementEndDate,
            statementEndBalance,
            ReconciliationTransactions
                .Where(transaction => transaction.CanChange && transaction.IsCleared)
                .Select(transaction => transaction.JournalLineId)
                .ToList());
        return true;
    }

    private void DisplayReconciliation(ReconciliationPreviewSummary preview)
    {
        var selectedIds = ReconciliationTransactions
            .Where(transaction => transaction.CanChange && transaction.IsCleared)
            .Select(transaction => transaction.JournalLineId)
            .ToHashSet();

        ReconciliationTransactions.Clear();
        foreach (var transaction in preview.Transactions)
        {
            var item = new ReconciliationTransactionItemViewModel(transaction);
            if (item.CanChange && selectedIds.Contains(item.JournalLineId))
            {
                item.IsCleared = true;
            }

            ReconciliationTransactions.Add(item);
        }

        ReconciliationClearedBalance = preview.ClearedBalance;
        ReconciliationDifference = preview.Difference;
    }

    private void RefreshReconciliationAccountOptions()
    {
        var selectedId = SelectedReconciliationAccount?.Id;
        ReconciliationAccountOptions.Clear();
        foreach (var account in Accounts.Where(account =>
                     account.IsActive
                     && account.AccountType is "Asset" or "Liability" or "Equity"))
        {
            ReconciliationAccountOptions.Add(account);
        }

        SelectedReconciliationAccount = ReconciliationAccountOptions
            .FirstOrDefault(account => account.Id == selectedId)
            ?? ReconciliationAccountOptions.FirstOrDefault();
    }

    private void RefreshImportPostingAccountOptions()
    {
        var selectedId = SelectedImportPostingAccount?.Id;
        ImportPostingAccountOptions.Clear();
        foreach (var account in Accounts.Where(account =>
                     account.IsActive
                     && account.AccountType is "Asset" or "Liability" or "Equity"))
        {
            ImportPostingAccountOptions.Add(account);
        }

        SelectedImportPostingAccount = ImportPostingAccountOptions
            .FirstOrDefault(account => account.Id == selectedId)
            ?? ImportPostingAccountOptions.FirstOrDefault();
    }

    private void RefreshImportFeeAccountOptions()
    {
        var selectedId = SelectedImportFeeAccount?.Id;
        ImportFeeAccountOptions.Clear();
        foreach (var account in Accounts.Where(account =>
                     account.IsActive && account.AccountType == "Expense"))
        {
            ImportFeeAccountOptions.Add(account);
        }

        SelectedImportFeeAccount = ImportFeeAccountOptions
            .FirstOrDefault(account => account.Id == selectedId)
            ?? ImportFeeAccountOptions.FirstOrDefault(account =>
                account.Name.Contains("fee", StringComparison.OrdinalIgnoreCase))
            ?? ImportFeeAccountOptions.FirstOrDefault();
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

    private bool TryCreateImportCommand(
        out Guid organizationId,
        out CsvImportCommand command)
    {
        organizationId = SelectedOrganization?.Id ?? Guid.Empty;
        command = null!;

        if (apiClient is null || SelectedOrganization is null)
        {
            WorkspaceMessage = "Select a company first.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(importCsvContent) || string.IsNullOrWhiteSpace(ImportFileName))
        {
            WorkspaceMessage = "Choose a CSV file first.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ImportSource))
        {
            WorkspaceMessage = "Enter an import source name.";
            return false;
        }

        var defaultCurrency = DefaultImportCurrency.Trim().ToUpperInvariant();
        if (defaultCurrency.Length != 3)
        {
            WorkspaceMessage = "Default currency must use a three-letter code.";
            return false;
        }

        var dateColumn = NormalizeOptionalColumn(SelectedDateColumn);
        var descriptionColumn = NormalizeOptionalColumn(SelectedDescriptionColumn);
        if (dateColumn is null || descriptionColumn is null)
        {
            WorkspaceMessage = "Map the date and description columns.";
            return false;
        }

        var amountColumn = NormalizeOptionalColumn(SelectedAmountColumn);
        var debitColumn = NormalizeOptionalColumn(SelectedDebitColumn);
        var creditColumn = NormalizeOptionalColumn(SelectedCreditColumn);
        if (amountColumn is null && debitColumn is null && creditColumn is null)
        {
            WorkspaceMessage = "Map an amount column or debit/credit columns.";
            return false;
        }

        if (amountColumn is not null && (debitColumn is not null || creditColumn is not null))
        {
            WorkspaceMessage = "Use either one amount column or debit/credit columns.";
            return false;
        }

        command = new CsvImportCommand(
            ImportSource.Trim(),
            ImportFileName,
            importCsvContent,
            new CsvColumnMappingCommand(
                dateColumn,
                descriptionColumn,
                amountColumn,
                debitColumn,
                creditColumn,
                NormalizeOptionalColumn(SelectedSourceIdColumn),
                NormalizeOptionalColumn(SelectedCurrencyColumn),
                defaultCurrency,
                NormalizeOptionalColumn(SelectedBalanceColumn),
                skipBalanceOnlyImportRows));
        return true;
    }

    private void ReplaceCsvHeaders(IEnumerable<string> headers)
    {
        CsvHeaders.Clear();
        CsvHeaders.Add(UnmappedColumn);
        foreach (var header in headers)
        {
            CsvHeaders.Add(header);
        }
    }

    private void ApplySuggestedMappings(IReadOnlyList<string> headers)
    {
        var mapping = CsvImportProfiles.Get(ImportSource).CreateMapping(headers);
        SelectedDateColumn = ToSelectedColumn(mapping.DateColumn);
        SelectedDescriptionColumn = ToSelectedColumn(mapping.DescriptionColumn);
        SelectedAmountColumn = mapping.AmountColumn ?? UnmappedColumn;
        SelectedDebitColumn = mapping.DebitColumn ?? UnmappedColumn;
        SelectedCreditColumn = mapping.CreditColumn ?? UnmappedColumn;
        SelectedSourceIdColumn = mapping.SourceTransactionIdColumn ?? UnmappedColumn;
        SelectedCurrencyColumn = mapping.CurrencyColumn ?? UnmappedColumn;
        SelectedBalanceColumn = mapping.BalanceColumn ?? UnmappedColumn;
        skipBalanceOnlyImportRows = mapping.SkipBalanceOnlyRows;
    }

    private static string ToSelectedColumn(string? column)
    {
        return string.IsNullOrWhiteSpace(column) ? UnmappedColumn : column;
    }

    private static string? NormalizeOptionalColumn(string? column)
    {
        return string.IsNullOrWhiteSpace(column) || column == UnmappedColumn ? null : column;
    }

    private void ClearDashboard()
    {
        DashboardIncomeSources.Clear();
        DashboardExpenseCategories.Clear();
        DashboardIncome = 0;
        DashboardExpenses = 0;
        DashboardNetIncome = 0;
    }

    private void RefreshTransactionAccountOptions()
    {
        RefreshImportPostingAccountOptions();
        RefreshImportFeeAccountOptions();
        RefreshReconciliationAccountOptions();
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
