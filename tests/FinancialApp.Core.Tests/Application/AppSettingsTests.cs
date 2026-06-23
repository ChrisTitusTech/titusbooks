using System.Text.Json;
using FinancialApp.Core.Application;

namespace FinancialApp.Core.Tests.Application;

public sealed class AppSettingsTests
{
    [Fact]
    public void DefaultsDoNotContainSecrets()
    {
        var settings = new AppSettings();

        Assert.Equal("TitusBooks", settings.ApplicationName);
        Assert.Equal("http://127.0.0.1:5000", settings.Api.BaseUrl);
        Assert.DoesNotContain("Database", JsonSerializer.Serialize(settings), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", JsonSerializer.Serialize(settings), StringComparison.OrdinalIgnoreCase);
    }
}
