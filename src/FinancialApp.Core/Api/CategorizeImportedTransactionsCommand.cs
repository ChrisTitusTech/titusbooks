namespace FinancialApp.Core.Api;

public sealed record CategorizeImportedTransactionsCommand(
    IReadOnlyList<Guid> TransactionIds,
    Guid CategoryAccountId);
