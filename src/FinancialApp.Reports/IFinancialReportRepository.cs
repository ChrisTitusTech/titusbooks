using FinancialApp.Core.Accounting;

namespace FinancialApp.Reports;

public interface IFinancialReportRepository
{
    Task<IReadOnlyList<AccountReportTotal>> ListAccountTotalsAsync(
        Guid organizationId,
        AccountType accountType,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}
