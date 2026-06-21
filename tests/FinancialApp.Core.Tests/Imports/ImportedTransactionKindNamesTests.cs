using FinancialApp.Core.Imports;

namespace FinancialApp.Core.Tests.Imports;

public sealed class ImportedTransactionKindNamesTests
{
    [Theory]
    [InlineData(ImportedTransactionKind.Other, "other")]
    [InlineData(ImportedTransactionKind.Payment, "payment")]
    [InlineData(ImportedTransactionKind.Fee, "fee")]
    [InlineData(ImportedTransactionKind.Refund, "refund")]
    [InlineData(ImportedTransactionKind.Transfer, "transfer")]
    [InlineData(ImportedTransactionKind.CurrencyConversion, "currency_conversion")]
    public void StorageValue_RoundTrips(
        ImportedTransactionKind kind,
        string storageValue)
    {
        Assert.Equal(storageValue, ImportedTransactionKindNames.ToStorageValue(kind));
        Assert.Equal(kind, ImportedTransactionKindNames.Parse(storageValue));
    }
}
