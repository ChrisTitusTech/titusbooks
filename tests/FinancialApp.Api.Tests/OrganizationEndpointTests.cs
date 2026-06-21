using System.Net;
using System.Net.Http.Json;
using FinancialApp.Api.Accounting;
using FinancialApp.Api.Categorization;
using FinancialApp.Api.Imports;
using FinancialApp.Api.Organizations;
using FinancialApp.Core.Accounting;
using FinancialApp.Core.Categorization;
using FinancialApp.Core.Imports;
using FinancialApp.Core.Organizations;
using FinancialApp.Importers;
using FinancialApp.Reports;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialApp.Api.Tests;

public sealed class OrganizationEndpointTests
{
    [Fact]
    public async Task CreateOrganization_CreatesOrganization()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/organizations", new CreateOrganizationRequest("Titus Books", "usd", 1));
        var body = await response.Content.ReadFromJsonAsync<OrganizationResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Titus Books", body.Name);
        Assert.Equal("USD", body.BaseCurrency);
        Assert.Single(repositories.Organizations);
    }

    [Fact]
    public async Task CreateOrganization_RejectsMissingName()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/organizations", new CreateOrganizationRequest("", "USD", 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(repositories.Organizations);
    }

    [Fact]
    public async Task SeedDefaultAccounts_CreatesAccountsOnce()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);

        var firstResponse = await client.PostAsync($"/organizations/{organizationId}/accounts/defaults", null);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<SeedAccountsResponse>();
        var secondResponse = await client.PostAsync($"/organizations/{organizationId}/accounts/defaults", null);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<SeedAccountsResponse>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstBody);
        Assert.Equal(DefaultChartOfAccounts.Templates.Count, firstBody.CreatedCount);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondBody);
        Assert.Equal(0, secondBody.CreatedCount);
    }

    [Fact]
    public async Task ListAccounts_ReturnsOrganizationAccounts()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        await client.PostAsync($"/organizations/{organizationId}/accounts/defaults", null);

        var response = await client.GetAsync($"/organizations/{organizationId}/accounts");
        var accounts = await response.Content.ReadFromJsonAsync<List<AccountResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(accounts);
        Assert.Contains(accounts, account => account.Name == "Checking");
        Assert.Contains(accounts, account => account.Name == "Merchant Fees");
    }

    [Fact]
    public async Task CreateAccount_CreatesAccount()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            new CreateAccountRequest("Bank Savings", "Asset", "Savings"));
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(account);
        Assert.Equal("Bank Savings", account.Name);
        Assert.Equal("Asset", account.AccountType);
    }

    [Fact]
    public async Task CreateAccount_ReturnsNotFoundForMissingOrganization()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/organizations/{Guid.NewGuid()}/accounts",
            new CreateAccountRequest("Bank Savings", "Asset", "Savings"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAccount_RenamesAccount()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var createResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            new CreateAccountRequest("Old Name", "Expense"));
        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var updateResponse = await client.PutAsJsonAsync(
            $"/organizations/{organizationId}/accounts/{createdAccount!.Id}",
            new UpdateAccountRequest("New Name", "Office"));
        var updatedAccount = await updateResponse.Content.ReadFromJsonAsync<AccountResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updatedAccount);
        Assert.Equal("New Name", updatedAccount.Name);
        Assert.Equal("Office", updatedAccount.AccountSubtype);
    }

    [Fact]
    public async Task UpdateAccount_RejectsDuplicateName()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            new CreateAccountRequest("Meals", "Expense"));
        var createResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            new CreateAccountRequest("Travel", "Expense"));
        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var updateResponse = await client.PutAsJsonAsync(
            $"/organizations/{organizationId}/accounts/{createdAccount!.Id}",
            new UpdateAccountRequest("Meals"));

        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
    }

    [Fact]
    public async Task DeactivateAccount_MarksAccountInactive()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var createResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            new CreateAccountRequest("Temporary", "Expense"));
        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var deactivateResponse = await client.PostAsync(
            $"/organizations/{organizationId}/accounts/{createdAccount!.Id}/deactivate",
            null);
        var deactivatedAccount = await deactivateResponse.Content.ReadFromJsonAsync<AccountResponse>();

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        Assert.NotNull(deactivatedAccount);
        Assert.False(deactivatedAccount.IsActive);
    }

    [Fact]
    public async Task PostExpense_CreatesBalancedJournalEntry()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var checking = await CreateAccountAsync(client, organizationId, "Checking", "Asset", "Checking");
        var supplies = await CreateAccountAsync(client, organizationId, "Office Supplies", "Expense");

        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/expenses",
            new PostExpenseRequest(
                new DateOnly(2026, 6, 17),
                checking.Id,
                supplies.Id,
                42.00m,
                "Office supplies"));
        var entry = await response.Content.ReadFromJsonAsync<JournalEntryResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(entry);
        Assert.Equal(42.00m, entry.TotalDebits);
        Assert.Equal(42.00m, entry.TotalCredits);
        Assert.Contains(entry.Lines, line => line.AccountId == supplies.Id && line.Debit == 42.00m);
        Assert.Contains(entry.Lines, line => line.AccountId == checking.Id && line.Credit == 42.00m);
    }

    [Fact]
    public async Task PostIncome_CreatesBalancedJournalEntry()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var checking = await CreateAccountAsync(client, organizationId, "Checking", "Asset", "Checking");
        var consulting = await CreateAccountAsync(client, organizationId, "Consulting Income", "Income");

        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/income",
            new PostIncomeRequest(
                new DateOnly(2026, 6, 17),
                checking.Id,
                consulting.Id,
                250.00m,
                "Consulting income"));
        var entry = await response.Content.ReadFromJsonAsync<JournalEntryResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(entry);
        Assert.Equal(250.00m, entry.TotalDebits);
        Assert.Equal(250.00m, entry.TotalCredits);
        Assert.Contains(entry.Lines, line => line.AccountId == checking.Id && line.Debit == 250.00m);
        Assert.Contains(entry.Lines, line => line.AccountId == consulting.Id && line.Credit == 250.00m);
    }

    [Fact]
    public async Task PostTransfer_RejectsSameAccount()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var checking = await CreateAccountAsync(client, organizationId, "Checking", "Asset", "Checking");

        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/transfers",
            new PostTransferRequest(
                new DateOnly(2026, 6, 17),
                checking.Id,
                checking.Id,
                25.00m,
                "Invalid transfer"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListRegister_ReturnsPostedAccountEntries()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var checking = await CreateAccountAsync(client, organizationId, "Checking", "Asset", "Checking");
        var supplies = await CreateAccountAsync(client, organizationId, "Office Supplies", "Expense");
        await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/expenses",
            new PostExpenseRequest(
                new DateOnly(2026, 6, 17),
                checking.Id,
                supplies.Id,
                42.00m,
                "Office supplies"));

        var response = await client.GetAsync($"/organizations/{organizationId}/accounts/{checking.Id}/register");
        var register = await response.Content.ReadFromJsonAsync<List<AccountRegisterEntryResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(register);
        var entry = Assert.Single(register);
        Assert.Equal(42.00m, entry.Credit);
        Assert.Equal("Office Supplies", entry.OtherAccounts);
    }

    [Fact]
    public async Task ProfitAndLoss_ReturnsPostedIncomeExpensesAndNetIncome()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var checking = await CreateAccountAsync(client, organizationId, "Checking", "Asset", "Checking");
        var consulting = await CreateAccountAsync(client, organizationId, "Consulting Income", "Income");
        var supplies = await CreateAccountAsync(client, organizationId, "Office Supplies", "Expense");
        await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/income",
            new PostIncomeRequest(new DateOnly(2026, 6, 10), checking.Id, consulting.Id, 500m, "Client payment"));
        await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/expenses",
            new PostExpenseRequest(new DateOnly(2026, 6, 11), checking.Id, supplies.Id, 75m, "Paper"));

        var response = await client.GetAsync(
            $"/organizations/{organizationId}/reports/profit-and-loss"
            + "?startDate=2026-06-01&endDate=2026-06-30");
        var report = await response.Content.ReadFromJsonAsync<FinancialApp.Api.Reporting.ProfitAndLossReportResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(report);
        Assert.Equal(500m, report.TotalIncome);
        Assert.Equal(75m, report.TotalExpenses);
        Assert.Equal(425m, report.NetIncome);
    }

    [Fact]
    public async Task ExpenseReportCsv_ReturnsCsvContent()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var checking = await CreateAccountAsync(client, organizationId, "Checking", "Asset", "Checking");
        var supplies = await CreateAccountAsync(client, organizationId, "Office Supplies", "Expense");
        await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/transactions/expenses",
            new PostExpenseRequest(new DateOnly(2026, 6, 11), checking.Id, supplies.Id, 75m, "Paper"));

        var response = await client.GetAsync(
            $"/organizations/{organizationId}/reports/expenses-by-category/csv"
            + "?startDate=2026-06-01&endDate=2026-06-30");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Expenses by Category,Office Supplies,75.00", csv);
    }

    [Fact]
    public async Task ProfitAndLoss_RejectsInvertedDateRange()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);

        var response = await client.GetAsync(
            $"/organizations/{organizationId}/reports/profit-and-loss"
            + "?startDate=2026-07-01&endDate=2026-06-30");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CsvImport_PreviewsImportsAndDetectsDuplicates()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var request = new CsvImportApiRequest(
            "Generic CSV",
            "fake.csv",
            """
            Date,Description,Amount,Balance
            2026-06-01,Office supplies,-42.00,958.00
            invalid,Bad date,10.00,968.00
            """,
            new CsvColumnMappingRequest(
                "Date",
                "Description",
                AmountColumn: "Amount",
                BalanceColumn: "Balance"));

        var previewResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/imports/csv/preview",
            request);
        var preview = await previewResponse.Content.ReadFromJsonAsync<CsvImportPreviewResponse>();
        var firstImportResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/imports/csv",
            request);
        var firstImport = await firstImportResponse.Content.ReadFromJsonAsync<CsvImportResultResponse>();
        var secondImportResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/imports/csv",
            request);
        var secondImport = await secondImportResponse.Content.ReadFromJsonAsync<CsvImportResultResponse>();
        var listResponse = await client.GetAsync(
            $"/organizations/{organizationId}/imports/transactions");
        var transactions = await listResponse.Content.ReadFromJsonAsync<List<ImportedTransactionResponse>>();

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.NotNull(preview);
        Assert.Equal(1, preview.ValidCount);
        Assert.Equal(1, preview.ErrorCount);
        Assert.Equal(HttpStatusCode.OK, firstImportResponse.StatusCode);
        Assert.NotNull(firstImport);
        Assert.Equal(1, firstImport.PendingCount);
        Assert.Equal(0, firstImport.DuplicateCount);
        Assert.NotNull(secondImport);
        Assert.Equal(0, secondImport.PendingCount);
        Assert.Equal(1, secondImport.DuplicateCount);
        Assert.NotNull(transactions);
        var transaction = Assert.Single(transactions);
        Assert.Equal("pending", transaction.Status);
        Assert.Equal(-42m, transaction.Amount);
        Assert.Equal(958m, transaction.Balance);
    }

    [Fact]
    public async Task CsvHeaders_ReturnsHeaderNames()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/imports/csv/headers",
            new CsvHeadersRequest("Date,Description,Amount\n2026-06-01,Test,1.00"));
        var headers = await response.Content.ReadFromJsonAsync<List<string>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["Date", "Description", "Amount"], headers);
    }

    [Fact]
    public async Task PayPalCsvImport_NormalizesCompletedBalanceTransactions()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var request = new CsvImportApiRequest(
            "PayPal",
            "paypal.csv",
            """
            Date,Time,TimeZone,Name,Type,Status,Currency,Gross,Fee,Net,Transaction ID,Reference Txn ID,Balance Impact
            06/01/2026,10:15:30,CDT,Fake Customer,Express Checkout Payment,Completed,USD,100.00,-3.49,96.51,PAY-001,,Credit
            06/02/2026,11:00:00,CDT,Pending Customer,Express Checkout Payment,Pending,USD,12.00,0.00,12.00,PENDING-001,,Credit
            """,
            new CsvColumnMappingRequest(
                "Date",
                "Name",
                AmountColumn: "Net",
                SourceTransactionIdColumn: "Transaction ID",
                CurrencyColumn: "Currency"));

        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/imports/csv",
            request);
        var result = await response.Content.ReadFromJsonAsync<CsvImportResultResponse>();
        var transactions = await client.GetFromJsonAsync<List<ImportedTransactionResponse>>(
            $"/organizations/{organizationId}/imports/transactions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(1, result.PendingCount);
        Assert.Equal(1, result.SkippedCount);
        var transaction = Assert.Single(transactions!);
        Assert.Equal("payment", transaction.Kind);
        Assert.Equal(100m, transaction.GrossAmount);
        Assert.Equal(3.49m, transaction.FeeAmount);
        Assert.Equal(96.51m, transaction.NetAmount);
    }

    [Fact]
    public async Task CategorizationRule_AppliesToFutureImports()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var category = await CreateAccountAsync(
            client,
            organizationId,
            "Office Supplies",
            "Expense");

        var createRuleResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/categorization-rules",
            new CreateCategorizationRuleRequest(
                "Office purchases",
                "description",
                "contains",
                "office",
                category.Id,
                10));
        var rule = await createRuleResponse.Content.ReadFromJsonAsync<CategorizationRuleResponse>();
        var importResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/imports/csv",
            new CsvImportApiRequest(
                "Generic CSV",
                "fake.csv",
                "Date,Description,Amount\n2026-06-01,ACME OFFICE STORE,-42.00",
                new CsvColumnMappingRequest(
                    "Date",
                    "Description",
                    AmountColumn: "Amount")));
        var importResult = await importResponse.Content.ReadFromJsonAsync<CsvImportResultResponse>();
        var transactions = await client.GetFromJsonAsync<List<ImportedTransactionResponse>>(
            $"/organizations/{organizationId}/imports/transactions");

        Assert.Equal(HttpStatusCode.Created, createRuleResponse.StatusCode);
        Assert.NotNull(rule);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        Assert.NotNull(importResult);
        Assert.Equal(0, importResult.PendingCount);
        Assert.Equal(1, importResult.CategorizedCount);
        var transaction = Assert.Single(transactions!);
        Assert.Equal("categorized", transaction.Status);
        Assert.Equal(category.Id, transaction.CategoryAccountId);
        Assert.Equal(rule.Id, transaction.MatchedRuleId);
    }

    [Fact]
    public async Task ImportInbox_CategorizesSelectedTransactionsAndFiltersByStatus()
    {
        var repositories = new InMemoryRepositories();
        await using var factory = CreateFactory(repositories);
        using var client = factory.CreateClient();
        var organizationId = await CreateOrganizationAsync(client);
        var category = await CreateAccountAsync(
            client,
            organizationId,
            "Office Supplies",
            "Expense");
        await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/imports/csv",
            new CsvImportApiRequest(
                "Generic CSV",
                "fake.csv",
                """
                Date,Description,Amount
                2026-06-01,Office store,-42.00
                2026-06-02,Coffee shop,-8.00
                """,
                new CsvColumnMappingRequest(
                    "Date",
                    "Description",
                    AmountColumn: "Amount")));
        var pending = await client.GetFromJsonAsync<List<ImportedTransactionResponse>>(
            $"/organizations/{organizationId}/imports/transactions?status=pending");

        var categorizeResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/imports/transactions/categorize",
            new CategorizeImportedTransactionsRequest(
                [pending![0].Id, pending[1].Id],
                category.Id));
        var categorized = await client.GetFromJsonAsync<List<ImportedTransactionResponse>>(
            $"/organizations/{organizationId}/imports/transactions?status=categorized");
        var remainingPending = await client.GetFromJsonAsync<List<ImportedTransactionResponse>>(
            $"/organizations/{organizationId}/imports/transactions?status=pending");

        Assert.Equal(HttpStatusCode.OK, categorizeResponse.StatusCode);
        Assert.Equal(2, categorized!.Count);
        Assert.All(categorized, transaction =>
        {
            Assert.Equal(category.Id, transaction.CategoryAccountId);
            Assert.Null(transaction.MatchedRuleId);
        });
        Assert.Empty(remainingPending!);
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/organizations",
            new CreateOrganizationRequest($"Test Organization {Guid.NewGuid():N}", "USD", 1));
        var organization = await response.Content.ReadFromJsonAsync<OrganizationResponse>();
        return organization!.Id;
    }

    private static async Task<AccountResponse> CreateAccountAsync(
        HttpClient client,
        Guid organizationId,
        string name,
        string accountType,
        string? accountSubtype = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/accounts",
            new CreateAccountRequest(name, accountType, accountSubtype));
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>();
        return account!;
    }

    private static WebApplicationFactory<Program> CreateFactory(InMemoryRepositories repositories)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IOrganizationRepository>();
                    services.RemoveAll<IAccountRepository>();
                    services.RemoveAll<IJournalEntryRepository>();
                    services.RemoveAll<DefaultChartOfAccountsSeeder>();
                    services.RemoveAll<AccountingService>();
                    services.RemoveAll<IFinancialReportRepository>();
                    services.RemoveAll<FinancialReportService>();
                    services.RemoveAll<IImportRepository>();
                    services.RemoveAll<ICategorizationRuleRepository>();
                    services.RemoveAll<CategorizationRuleEngine>();
                    services.RemoveAll<GenericCsvParser>();
                    services.RemoveAll<PayPalCsvParser>();
                    services.RemoveAll<CsvImportService>();
                    services.AddSingleton<IOrganizationRepository>(repositories);
                    services.AddSingleton<IAccountRepository>(repositories);
                    services.AddSingleton<IJournalEntryRepository>(repositories);
                    services.AddSingleton<DefaultChartOfAccountsSeeder>();
                    services.AddSingleton<AccountingService>();
                    services.AddSingleton<IFinancialReportRepository>(repositories);
                    services.AddSingleton<FinancialReportService>();
                    services.AddSingleton<IImportRepository>(repositories);
                    services.AddSingleton<ICategorizationRuleRepository>(repositories);
                    services.AddSingleton<CategorizationRuleEngine>();
                    services.AddSingleton<GenericCsvParser>();
                    services.AddSingleton<PayPalCsvParser>();
                    services.AddSingleton<CsvImportService>();
                });
            });
    }

    private sealed class InMemoryRepositories :
        IOrganizationRepository,
        IAccountRepository,
        IJournalEntryRepository,
        IImportRepository,
        ICategorizationRuleRepository,
        IFinancialReportRepository
    {
        private readonly List<Account> accounts = [];
        private readonly List<JournalEntry> journalEntries = [];
        private readonly List<ImportedTransaction> importedTransactions = [];
        private readonly List<CategorizationRule> categorizationRules = [];

        public List<Organization> Organizations { get; } = [];

        public Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
        {
            Organizations.Add(organization);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Organization>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Organization>>(Organizations.ToList());
        }

        public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var organization = Organizations.SingleOrDefault(organization => organization.Id == id);
            return Task.FromResult(organization);
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
            var index = accounts.FindIndex(existingAccount =>
                existingAccount.OrganizationId == account.OrganizationId
                && existingAccount.Id == account.Id);

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

        public Task<IReadOnlyList<Account>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Account>>(
                accounts.Where(account => account.OrganizationId == organizationId).ToList());
        }

        public Task AddAsync(JournalEntry journalEntry, CancellationToken cancellationToken = default)
        {
            journalEntries.Add(journalEntry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AccountRegisterEntry>> ListRegisterAsync(
            Guid organizationId,
            Guid accountId,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            CancellationToken cancellationToken = default)
        {
            var registerEntries = journalEntries
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
                        string.Join(", ", entry.Lines
                            .Where(otherLine => otherLine.AccountId != accountId)
                            .Select(otherLine => accounts.Single(account => account.Id == otherLine.AccountId).Name)))))
                .ToList();

            return Task.FromResult<IReadOnlyList<AccountRegisterEntry>>(registerEntries);
        }

        public Task<IReadOnlyList<AccountReportTotal>> ListAccountTotalsAsync(
            Guid organizationId,
            AccountType accountType,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
        {
            var totals = journalEntries
                .Where(entry => entry.OrganizationId == organizationId && !entry.IsVoid)
                .Where(entry => entry.EntryDate >= startDate && entry.EntryDate <= endDate)
                .SelectMany(entry => entry.Lines)
                .Join(
                    accounts.Where(account =>
                        account.OrganizationId == organizationId
                        && account.AccountType == accountType),
                    line => line.AccountId,
                    account => account.Id,
                    (line, account) => new { Line = line, Account = account })
                .GroupBy(item => item.Account)
                .Select(group => new AccountReportTotal(
                    group.Key.Id,
                    group.Key.Name,
                    accountType == AccountType.Income
                        ? group.Sum(item => item.Line.Credit - item.Line.Debit)
                        : group.Sum(item => item.Line.Debit - item.Line.Credit)))
                .Where(total => total.Amount != 0)
                .OrderBy(total => total.AccountName)
                .ToList();

            return Task.FromResult<IReadOnlyList<AccountReportTotal>>(totals);
        }

        public Task<IReadOnlySet<string>> FindExistingFingerprintsAsync(
            Guid organizationId,
            IReadOnlyCollection<string> fingerprints,
            CancellationToken cancellationToken = default)
        {
            IReadOnlySet<string> existing = importedTransactions
                .Where(transaction =>
                    transaction.OrganizationId == organizationId
                    && fingerprints.Contains(transaction.Fingerprint))
                .Select(transaction => transaction.Fingerprint)
                .ToHashSet(StringComparer.Ordinal);
            return Task.FromResult(existing);
        }

        public Task AddBatchAsync(
            ImportBatch batch,
            IReadOnlyCollection<ImportedTransaction> transactions,
            CancellationToken cancellationToken = default)
        {
            importedTransactions.AddRange(transactions);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ImportedTransaction>> ListTransactionsAsync(
            Guid organizationId,
            ImportedTransactionStatus? status = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ImportedTransaction>>(
                importedTransactions
                    .Where(transaction => transaction.OrganizationId == organizationId)
                    .Where(transaction => status is null || transaction.Status == status)
                    .ToList());
        }

        public Task<bool> CategorizeTransactionsAsync(
            Guid organizationId,
            IReadOnlyCollection<Guid> transactionIds,
            Guid categoryAccountId,
            CancellationToken cancellationToken = default)
        {
            var distinctIds = transactionIds.Distinct().ToHashSet();
            var matches = importedTransactions
                .Where(transaction =>
                    transaction.OrganizationId == organizationId
                    && distinctIds.Contains(transaction.Id)
                    && transaction.Status is ImportedTransactionStatus.Pending
                        or ImportedTransactionStatus.Categorized)
                .ToList();
            if (matches.Count != distinctIds.Count)
            {
                return Task.FromResult(false);
            }

            foreach (var match in matches)
            {
                var index = importedTransactions.IndexOf(match);
                importedTransactions[index] = match with
                {
                    CategoryAccountId = categoryAccountId,
                    MatchedRuleId = null,
                    Status = ImportedTransactionStatus.Categorized
                };
            }

            return Task.FromResult(true);
        }

        public Task AddAsync(
            CategorizationRule rule,
            CancellationToken cancellationToken = default)
        {
            categorizationRules.Add(rule);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CategorizationRule>> ListAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CategorizationRule>>(
                categorizationRules
                    .Where(rule => rule.OrganizationId == organizationId)
                    .OrderBy(rule => rule.Priority)
                    .ToList());
        }

        public Task<IReadOnlyList<CategorizationRule>> ListActiveAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CategorizationRule>>(
                categorizationRules
                    .Where(rule => rule.OrganizationId == organizationId && rule.IsActive)
                    .OrderBy(rule => rule.Priority)
                    .ToList());
        }
    }
}
