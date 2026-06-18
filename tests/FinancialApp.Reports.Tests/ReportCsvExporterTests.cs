using FinancialApp.Reports;

namespace FinancialApp.Reports.Tests;

public sealed class ReportCsvExporterTests
{
    [Fact]
    public void Export_ProfitAndLoss_IncludesTotalsAndEscapesAccountNames()
    {
        var report = new ProfitAndLossReport(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30),
            [new AccountReportTotal(Guid.NewGuid(), "Sales, Online", 500m)],
            [new AccountReportTotal(Guid.NewGuid(), "Merchant Fees", 20m)]);

        var csv = ReportCsvExporter.Export(report);

        Assert.Contains("Income,\"Sales, Online\",500.00", csv);
        Assert.Contains("Total,Total Income,500.00", csv);
        Assert.Contains("Total,Total Expenses,20.00", csv);
        Assert.Contains("Total,Net Income,480.00", csv);
    }

    [Fact]
    public void Export_AccountBreakdown_IncludesReportDateRange()
    {
        var report = new AccountBreakdownReport(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            [new AccountReportTotal(Guid.NewGuid(), "Office Supplies", 42m)]);

        var csv = ReportCsvExporter.Export("Expenses by Category", report);

        Assert.Contains("Report,Expenses by Category", csv);
        Assert.Contains("Start Date,2026-06-01", csv);
        Assert.Contains("End Date,2026-06-30", csv);
        Assert.Contains("Expenses by Category,Office Supplies,42.00", csv);
    }
}
