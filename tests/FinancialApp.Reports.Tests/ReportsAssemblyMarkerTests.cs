using FinancialApp.Reports;

namespace FinancialApp.Reports.Tests;

public sealed class ReportsAssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerIsAvailable()
    {
        Assert.Equal("FinancialApp.Reports", typeof(ReportsAssemblyMarker).Namespace);
    }
}
