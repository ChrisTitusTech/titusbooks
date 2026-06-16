using FinancialApp.Core.Application;

namespace FinancialApp.Core.Tests.Application;

public sealed class AppSettingsTests
{
    [Fact]
    public void DefaultsDoNotContainSecrets()
    {
        var settings = new AppSettings();

        Assert.Equal("TitusBooks", settings.ApplicationName);
        Assert.Equal("localhost", settings.Database.Host);
        Assert.Equal("titusbooks", settings.Database.DatabaseName);
    }
}
