namespace FinancialApp.Core.Imports;

public sealed record ImportedTransaction
{
    public required Guid Id { get; init; }

    public required Guid OrganizationId { get; init; }

    public Guid? ImportBatchId { get; init; }

    public required string Source { get; init; }

    public string? SourceTransactionId { get; init; }

    public string? ReferenceTransactionId { get; init; }

    public string? SourceType { get; init; }

    public string? SourceStatus { get; init; }

    public required DateOnly PostedDate { get; init; }

    public TimeOnly? PostedTime { get; init; }

    public string? SourceTimeZone { get; init; }

    public required string Description { get; init; }

    public string? RawDescription { get; init; }

    public required decimal Amount { get; init; }

    public decimal? Balance { get; init; }

    public decimal? GrossAmount { get; init; }

    public decimal? FeeAmount { get; init; }

    public decimal? NetAmount { get; init; }

    public ImportedTransactionKind Kind { get; init; } = ImportedTransactionKind.Other;

    public string Currency { get; init; } = "USD";

    public ImportedTransactionStatus Status { get; init; } = ImportedTransactionStatus.Pending;

    public required string Fingerprint { get; init; }

    public string? RawPayloadJson { get; init; }
}
