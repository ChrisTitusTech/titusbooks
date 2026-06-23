namespace FinancialApp.Core.Application;

public sealed record DesktopUserSettings
{
    public ApiSettings Api { get; init; } = new();
}
