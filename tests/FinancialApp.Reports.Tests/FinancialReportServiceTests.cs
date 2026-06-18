using FinancialApp.Core.Accounting;
using FinancialApp.Reports;

namespace FinancialApp.Reports.Tests;

public sealed class FinancialReportServiceTests
{
    [Fact]
    public async Task GetProfitAndLossAsync_CalculatesTotalsAndNetIncome()
    {
        var repository = new StubReportRepository
        {
            Income =
            [
                new AccountReportTotal(Guid.NewGuid(), "Consulting Income", 1000m),
                new AccountReportTotal(Guid.NewGuid(), "Sales Income", 250m)
            ],
            Expenses =
            [
                new AccountReportTotal(Guid.NewGuid(), "Office Supplies", 100m),
                new AccountReportTotal(Guid.NewGuid(), "Merchant Fees", 25m)
            ]
        };
        var service = new FinancialReportService(repository);

        var report = await service.GetProfitAndLossAsync(
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));

        Assert.Equal(1250m, report.TotalIncome);
        Assert.Equal(125m, report.TotalExpenses);
        Assert.Equal(1125m, report.NetIncome);
    }

    [Fact]
    public async Task GetExpenseByCategoryAsync_ReturnsExpenseAccounts()
    {
        var repository = new StubReportRepository
        {
            Expenses =
            [
                new AccountReportTotal(Guid.NewGuid(), "Travel", 320m),
                new AccountReportTotal(Guid.NewGuid(), "Meals", 80m)
            ]
        };
        var service = new FinancialReportService(repository);

        var report = await service.GetExpenseByCategoryAsync(
            Guid.NewGuid(),
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        Assert.Equal(400m, report.Total);
        Assert.Equal(["Travel", "Meals"], report.Accounts.Select(account => account.AccountName));
    }

    [Fact]
    public async Task GetProfitAndLossAsync_RejectsInvertedDateRange()
    {
        var service = new FinancialReportService(new StubReportRepository());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetProfitAndLossAsync(
                Guid.NewGuid(),
                new DateOnly(2026, 7, 1),
                new DateOnly(2026, 6, 30)));

        Assert.Equal("Start date must be on or before end date.", exception.Message);
    }

    private sealed class StubReportRepository : IFinancialReportRepository
    {
        public IReadOnlyList<AccountReportTotal> Income { get; init; } = [];

        public IReadOnlyList<AccountReportTotal> Expenses { get; init; } = [];

        public Task<IReadOnlyList<AccountReportTotal>> ListAccountTotalsAsync(
            Guid organizationId,
            AccountType accountType,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(accountType == AccountType.Income ? Income : Expenses);
        }
    }
}
