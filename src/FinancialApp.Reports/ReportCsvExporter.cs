using System.Globalization;
using System.Text;

namespace FinancialApp.Reports;

public static class ReportCsvExporter
{
    public static string Export(ProfitAndLossReport report)
    {
        var csv = CreateHeader("Profit and Loss", report.StartDate, report.EndDate);
        AppendSection(csv, "Income", report.Income);
        AppendTotal(csv, "Total Income", report.TotalIncome);
        AppendSection(csv, "Expenses", report.Expenses);
        AppendTotal(csv, "Total Expenses", report.TotalExpenses);
        AppendTotal(csv, "Net Income", report.NetIncome);
        return csv.ToString();
    }

    public static string Export(string title, AccountBreakdownReport report)
    {
        var csv = CreateHeader(title, report.StartDate, report.EndDate);
        AppendSection(csv, title, report.Accounts);
        AppendTotal(csv, "Total", report.Total);
        return csv.ToString();
    }

    private static StringBuilder CreateHeader(string title, DateOnly startDate, DateOnly endDate)
    {
        var csv = new StringBuilder();
        csv.AppendLine($"Report,{Escape(title)}");
        csv.AppendLine($"Start Date,{startDate:yyyy-MM-dd}");
        csv.AppendLine($"End Date,{endDate:yyyy-MM-dd}");
        csv.AppendLine();
        csv.AppendLine("Section,Account,Amount");
        return csv;
    }

    private static void AppendSection(
        StringBuilder csv,
        string section,
        IEnumerable<AccountReportTotal> accounts)
    {
        foreach (var account in accounts)
        {
            csv.Append(Escape(section));
            csv.Append(',');
            csv.Append(Escape(account.AccountName));
            csv.Append(',');
            csv.AppendLine(account.Amount.ToString("0.00", CultureInfo.InvariantCulture));
        }
    }

    private static void AppendTotal(StringBuilder csv, string label, decimal amount)
    {
        csv.Append("Total,");
        csv.Append(Escape(label));
        csv.Append(',');
        csv.AppendLine(amount.ToString("0.00", CultureInfo.InvariantCulture));
    }

    private static string Escape(string value)
    {
        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
