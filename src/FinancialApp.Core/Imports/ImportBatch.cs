namespace FinancialApp.Core.Imports;

public sealed record ImportBatch
{
    public required Guid Id { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string Source { get; init; }

    public string? FileName { get; init; }

    public string? FileHash { get; init; }

    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? RawMetadataJson { get; init; }
}
