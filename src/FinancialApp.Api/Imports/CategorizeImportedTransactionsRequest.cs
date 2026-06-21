namespace FinancialApp.Api.Imports;

public sealed record CategorizeImportedTransactionsRequest(
    IReadOnlyList<Guid> TransactionIds,
    Guid CategoryAccountId);
