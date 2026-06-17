namespace FinancialApp.Core.Api;

public sealed record SeedAccountsResult(Guid OrganizationId, int CreatedCount, IReadOnlyList<AccountSummary> CreatedAccounts);
