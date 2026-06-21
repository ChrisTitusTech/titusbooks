namespace FinancialApp.Core.Accounting;

public sealed record PayPalSale(
    Guid OrganizationId,
    DateOnly EntryDate,
    Guid PayPalAccountId,
    Guid IncomeAccountId,
    Guid MerchantFeeAccountId,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal NetAmount,
    string? Memo = null,
    Guid? SourceImportedTransactionId = null);
