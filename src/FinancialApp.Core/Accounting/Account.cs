namespace FinancialApp.Core.Accounting;

public sealed record Account
{
    public required Guid Id { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string Name { get; init; }

    public required AccountType AccountType { get; init; }

    public string? AccountSubtype { get; init; }

    public string Currency { get; init; } = "USD";

    public Guid? ParentAccountId { get; init; }

    public bool IsActive { get; init; } = true;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
