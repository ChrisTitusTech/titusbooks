namespace FinancialApp.Core.Api;

public sealed record ApiHealthResponse(string Status, string Service, DateTimeOffset CheckedAt);
