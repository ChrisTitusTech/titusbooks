namespace FinancialApp.Core.Organizations;

public sealed record Organization
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string BaseCurrency { get; init; } = "USD";

    public int FiscalYearStartMonth { get; init; } = 1;

    public string AccountingMethod { get; init; } = "cash";

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
