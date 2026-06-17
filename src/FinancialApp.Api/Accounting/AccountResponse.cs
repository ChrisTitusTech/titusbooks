using FinancialApp.Core.Accounting;

namespace FinancialApp.Api.Accounting;

public sealed record AccountResponse(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string AccountType,
    string? AccountSubtype,
    string Currency,
    bool IsActive)
{
    public static AccountResponse FromAccount(Account account)
    {
        return new AccountResponse(
            account.Id,
            account.OrganizationId,
            account.Name,
            account.AccountType.ToString(),
            account.AccountSubtype,
            account.Currency,
            account.IsActive);
    }
}
