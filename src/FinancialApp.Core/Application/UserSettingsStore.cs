using System.Text.Json;

namespace FinancialApp.Core.Application;

public sealed class UserSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string filePath;

    public UserSettingsStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    public string FilePath => filePath;

    public async Task SaveApiSettingsAsync(ApiSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ApiEndpointValidator.Validate(settings.BaseUrl, settings.AllowInsecureRemoteHttp);

        if (settings.RequestTimeoutSeconds is < 5 or > 300)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "API request timeout must be between 5 and 300 seconds.");
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = filePath + ".tmp";
        var userSettings = new DesktopUserSettings { Api = settings };

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    userSettings,
                    SerializerOptions,
                    cancellationToken);
            }

            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
