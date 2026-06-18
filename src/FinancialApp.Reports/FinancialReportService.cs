using FinancialApp.Core.Accounting;

namespace FinancialApp.Reports;

public sealed class FinancialReportService
{
    private readonly IFinancialReportRepository repository;

    public FinancialReportService(IFinancialReportRepository repository)
    {
        this.repository = repository;
    }

    public async Task<ProfitAndLossReport> GetProfitAndLossAsync(
        Guid organizationId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(startDate, endDate);

        var incomeTask = repository.ListAccountTotalsAsync(
            organizationId,
            AccountType.Income,
            startDate,
            endDate,
            cancellationToken);
        var expenseTask = repository.ListAccountTotalsAsync(
            organizationId,
            AccountType.Expense,
            startDate,
            endDate,
            cancellationToken);

        await Task.WhenAll(incomeTask, expenseTask);

        return new ProfitAndLossReport(
            startDate,
            endDate,
            await incomeTask,
            await expenseTask);
    }

    public async Task<AccountBreakdownReport> GetExpenseByCategoryAsync(
        Guid organizationId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await GetAccountBreakdownAsync(
            organizationId,
            AccountType.Expense,
            startDate,
            endDate,
            cancellationToken);
    }

    public async Task<AccountBreakdownReport> GetIncomeBySourceAsync(
        Guid organizationId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await GetAccountBreakdownAsync(
            organizationId,
            AccountType.Income,
            startDate,
            endDate,
            cancellationToken);
    }

    private async Task<AccountBreakdownReport> GetAccountBreakdownAsync(
        Guid organizationId,
        AccountType accountType,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        ValidateDateRange(startDate, endDate);

        var accounts = await repository.ListAccountTotalsAsync(
            organizationId,
            accountType,
            startDate,
            endDate,
            cancellationToken);

        return new AccountBreakdownReport(startDate, endDate, accounts);
    }

    private static void ValidateDateRange(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException("Start date must be on or before end date.");
        }
    }
}
