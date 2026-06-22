using System.Net;
using FinancialApp.Core.Api;

namespace FinancialApp.Core.Tests.Api;

public sealed class TitusBooksApiClientTests
{
    [Fact]
    public async Task ListOrganizationsAsync_ReturnsOrganizations()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """[{"id":"11111111-1111-1111-1111-111111111111","name":"Titus Books","baseCurrency":"USD","fiscalYearStartMonth":1,"accountingMethod":"cash"}]""")
            }))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };
        var client = new TitusBooksApiClient(httpClient);

        var organizations = await client.ListOrganizationsAsync();

        Assert.Single(organizations);
        Assert.Equal("Titus Books", organizations[0].Name);
    }

    [Fact]
    public async Task CreateAccountAsync_ReturnsCreatedAccount()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/organizations/11111111-1111-1111-1111-111111111111/accounts", request.RequestUri?.AbsolutePath);

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """{"id":"22222222-2222-2222-2222-222222222222","organizationId":"11111111-1111-1111-1111-111111111111","name":"Office Supplies","accountType":"Expense","accountSubtype":null,"currency":"USD","isActive":true}""")
            };
        }))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };
        var client = new TitusBooksApiClient(httpClient);

        var account = await client.CreateAccountAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new CreateAccountCommand("Office Supplies", "Expense"));

        Assert.Equal("Office Supplies", account.Name);
        Assert.Equal("Expense", account.AccountType);
        Assert.True(account.IsActive);
    }

    [Fact]
    public async Task UpdateAccountAsync_ReturnsUpdatedAccount()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal(
                "/organizations/11111111-1111-1111-1111-111111111111/accounts/22222222-2222-2222-2222-222222222222",
                request.RequestUri?.AbsolutePath);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"id":"22222222-2222-2222-2222-222222222222","organizationId":"11111111-1111-1111-1111-111111111111","name":"Office Supplies Updated","accountType":"Expense","accountSubtype":"Office","currency":"USD","isActive":true}""")
            };
        }))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };
        var client = new TitusBooksApiClient(httpClient);

        var account = await client.UpdateAccountAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            new UpdateAccountCommand("Office Supplies Updated", "Office"));

        Assert.Equal("Office Supplies Updated", account.Name);
        Assert.Equal("Office", account.AccountSubtype);
    }

    [Fact]
    public async Task CreateAccountAsync_ThrowsApiErrorMessage()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"Account name is required."}""")
            }))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };
        var client = new TitusBooksApiClient(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateAccountAsync(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                new CreateAccountCommand("", "Expense")));

        Assert.Equal("Account name is required.", exception.Message);
    }

    [Fact]
    public async Task PostExpenseAsync_ReturnsJournalEntry()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                "/organizations/11111111-1111-1111-1111-111111111111/transactions/expenses",
                request.RequestUri?.AbsolutePath);

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """{"id":"33333333-3333-3333-3333-333333333333","organizationId":"11111111-1111-1111-1111-111111111111","entryDate":"2026-06-17","memo":"Office supplies","totalDebits":42.00,"totalCredits":42.00,"lines":[]}""")
            };
        }))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };
        var client = new TitusBooksApiClient(httpClient);

        var entry = await client.PostExpenseAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new PostExpenseCommand(
                new DateOnly(2026, 6, 17),
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                42.00m,
                "Office supplies"));

        Assert.Equal(42.00m, entry.TotalDebits);
        Assert.Equal(42.00m, entry.TotalCredits);
    }

    [Fact]
    public async Task ListRegisterAsync_ReturnsEntries()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                "/organizations/11111111-1111-1111-1111-111111111111/accounts/22222222-2222-2222-2222-222222222222/register",
                request.RequestUri?.AbsolutePath);
            Assert.Equal("?startDate=2026-06-01&endDate=2026-06-30", request.RequestUri?.Query);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """[{"journalEntryId":"33333333-3333-3333-3333-333333333333","entryDate":"2026-06-17","memo":"Office supplies","debit":0,"credit":42.00,"otherAccounts":"Office Supplies"}]""")
            };
        }))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };
        var client = new TitusBooksApiClient(httpClient);

        var entries = await client.ListRegisterAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        var entry = Assert.Single(entries);
        Assert.Equal(42.00m, entry.Credit);
        Assert.Equal("Office Supplies", entry.OtherAccounts);
    }

    [Fact]
    public async Task GetProfitAndLossAsync_ReturnsReport()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                "/organizations/11111111-1111-1111-1111-111111111111/reports/profit-and-loss",
                request.RequestUri?.AbsolutePath);
            Assert.Equal("?startDate=2026-01-01&endDate=2026-06-30", request.RequestUri?.Query);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"startDate":"2026-01-01","endDate":"2026-06-30","income":[],"expenses":[],"totalIncome":500.00,"totalExpenses":75.00,"netIncome":425.00}""")
            };
        }))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };
        var client = new TitusBooksApiClient(httpClient);

        var report = await client.GetProfitAndLossAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));

        Assert.Equal(500m, report.TotalIncome);
        Assert.Equal(75m, report.TotalExpenses);
        Assert.Equal(425m, report.NetIncome);
    }

    [Fact]
    public async Task PreviewReconciliationAsync_PostsSelectionAndReturnsDifference()
    {
        var organizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var accountId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var lineId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                $"/organizations/{organizationId}/accounts/{accountId}/reconciliation/preview",
                request.RequestUri?.AbsolutePath);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""
                    {
                      "accountId":"{{accountId}}",
                      "statementEndDate":"2026-06-30",
                      "statementEndBalance":100.00,
                      "clearedBalance":100.00,
                      "difference":0.00,
                      "transactions":[]
                    }
                    """)
            };
        }))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };
        var client = new TitusBooksApiClient(httpClient);

        var preview = await client.PreviewReconciliationAsync(
            organizationId,
            accountId,
            new ReconciliationCommand(
                new DateOnly(2026, 6, 30),
                100m,
                [lineId]));

        Assert.Equal(100m, preview.ClearedBalance);
        Assert.Equal(0m, preview.Difference);
    }

    [Fact]
    public async Task CreateCategorizationRuleAsync_ReturnsCreatedRule()
    {
        var organizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                $"/organizations/{organizationId}/categorization-rules",
                request.RequestUri?.AbsolutePath);

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    $$"""
                    {
                      "id":"22222222-2222-2222-2222-222222222222",
                      "organizationId":"{{organizationId}}",
                      "name":"Office store",
                      "matchField":"description",
                      "matchOperator":"contains",
                      "matchValue":"Office store",
                      "targetAccountId":"33333333-3333-3333-3333-333333333333",
                      "priority":100,
                      "isActive":true
                    }
                    """)
            };
        }))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };
        var client = new TitusBooksApiClient(httpClient);

        var rule = await client.CreateCategorizationRuleAsync(
            organizationId,
            new CreateCategorizationRuleCommand(
                "Office store",
                "description",
                "contains",
                "Office store",
                Guid.Parse("33333333-3333-3333-3333-333333333333")));

        Assert.Equal("Office store", rule.Name);
        Assert.Equal("contains", rule.MatchOperator);
        Assert.Equal(organizationId, rule.OrganizationId);
    }

    [Fact]
    public async Task PostImportedTransactionsAsync_ReturnsPostingResult()
    {
        var organizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                $"/organizations/{organizationId}/imports/transactions/post",
                request.RequestUri?.AbsolutePath);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"postedCount":1,"journalEntryIds":["44444444-4444-4444-4444-444444444444"]}""")
            };
        }))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000")
        };
        var client = new TitusBooksApiClient(httpClient);

        var result = await client.PostImportedTransactionsAsync(
            organizationId,
            new PostImportedTransactionsCommand(
                [Guid.Parse("22222222-2222-2222-2222-222222222222")],
                Guid.Parse("33333333-3333-3333-3333-333333333333")));

        Assert.Equal(1, result.PostedCount);
        Assert.Single(result.JournalEntryIds);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
