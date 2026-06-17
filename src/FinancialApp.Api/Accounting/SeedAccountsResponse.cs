namespace FinancialApp.Api.Accounting;

public sealed record SeedAccountsResponse(Guid OrganizationId, int CreatedCount, IReadOnlyList<AccountResponse> CreatedAccounts);
