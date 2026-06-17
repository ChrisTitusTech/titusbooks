using FinancialApp.Core.Organizations;

namespace FinancialApp.Api.Organizations;

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string BaseCurrency,
    int FiscalYearStartMonth,
    string AccountingMethod)
{
    public static OrganizationResponse FromOrganization(Organization organization)
    {
        return new OrganizationResponse(
            organization.Id,
            organization.Name,
            organization.BaseCurrency,
            organization.FiscalYearStartMonth,
            organization.AccountingMethod);
    }
}
