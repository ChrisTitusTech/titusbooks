using FinancialApp.Importers;

namespace FinancialApp.Importers.Tests;

public sealed class ImportersAssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerIsAvailable()
    {
        Assert.Equal("FinancialApp.Importers", typeof(ImportersAssemblyMarker).Namespace);
    }
}
