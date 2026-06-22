namespace FinancialApp.Api.Imports;

public sealed record PostImportedTransactionsRequest(
    IReadOnlyList<Guid> TransactionIds,
    Guid SourceAccountId,
    Guid? MerchantFeeAccountId = null);
