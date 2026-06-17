namespace FinancialApp.Api.Health;

public sealed record HealthResponse(string Status, string Service, DateTimeOffset CheckedAt);
