namespace FinancialApp.Core.Api;

public sealed record PostImportedTransactionsCommand(
    IReadOnlyList<Guid> TransactionIds,
    Guid SourceAccountId,
    Guid? MerchantFeeAccountId = null);
