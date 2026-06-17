using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinancialApp.Core.Api;
using FinancialApp.Core.Application;

namespace FinancialApp.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
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

    private readonly TitusBooksApiClient? apiClient;
    private int accountLoadVersion;

    public MainWindowViewModel()
        : this(new AppSettings())
    {
    }

    public MainWindowViewModel(AppSettings settings, TitusBooksApiClient? apiClient = null)
    {
        this.apiClient = apiClient;
        Title = settings.ApplicationName;
        StatusMessage = "Company setup";
        DatabaseSummary = $"API target: {settings.Api.BaseUrl}";
    }

    public string Title { get; }

    public string StatusMessage { get; }

    public string DatabaseSummary { get; }

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

    public async Task CheckApiHealthAsync(ApiHealthClient apiHealthClient, CancellationToken cancellationToken = default)
    {
        var health = await apiHealthClient.CheckHealthAsync(cancellationToken);
        ApiHealthMessage = health.Message;
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadOrganizationsAsync();
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
                Accounts.Clear();
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
            Accounts.Clear();
            SelectedAccount = null;
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

            Accounts.Clear();
            foreach (var account in accounts)
            {
                Accounts.Add(account);
            }

            SelectedAccount = Accounts.FirstOrDefault();
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

    partial void OnSelectedOrganizationChanged(OrganizationSummary? value)
    {
        if (value is null)
        {
            Accounts.Clear();
            SelectedAccount = null;
            return;
        }

        _ = LoadAccountsForOrganizationAsync(value);
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
            return;
        }

        Accounts[existingIndex.Value] = account;
    }
}
