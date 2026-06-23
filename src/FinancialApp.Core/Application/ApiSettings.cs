namespace FinancialApp.Core.Application;

public sealed record ApiSettings
{
    public string BaseUrl { get; init; } = "http://127.0.0.1:5000";

    public int RequestTimeoutSeconds { get; init; } = 30;

    public bool AllowInsecureRemoteHttp { get; init; }
}
