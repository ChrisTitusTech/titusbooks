using System.Text.Json;
using FinancialApp.Core.Application;

namespace FinancialApp.Core.Tests.Application;

public sealed class UserSettingsStoreTests
{
    [Fact]
    public async Task SaveApiSettingsAsync_PersistsOnlyDesktopSafeApiConfiguration()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"titusbooks-tests-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "settings.json");

        try
        {
            var store = new UserSettingsStore(filePath);

            await store.SaveApiSettingsAsync(new ApiSettings
            {
                BaseUrl = "https://books.example.test",
                RequestTimeoutSeconds = 45,
            });

            var json = await File.ReadAllTextAsync(filePath);
            using var document = JsonDocument.Parse(json);

            Assert.Equal(
                "https://books.example.test",
                document.RootElement.GetProperty("Api").GetProperty("BaseUrl").GetString());
            Assert.DoesNotContain("Database", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ConnectionString", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
