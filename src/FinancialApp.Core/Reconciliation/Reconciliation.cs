namespace FinancialApp.Core.Reconciliation;

public sealed record Reconciliation
{
    public required Guid Id { get; init; }

    public required Guid OrganizationId { get; init; }

    public required Guid AccountId { get; init; }

    public required DateOnly StatementEndDate { get; init; }

    public required decimal StatementEndBalance { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }
}
