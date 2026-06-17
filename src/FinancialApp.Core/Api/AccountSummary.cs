namespace FinancialApp.Core.Api;

public sealed record AccountSummary(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string AccountType,
    string? AccountSubtype,
    string Currency,
    bool IsActive);
