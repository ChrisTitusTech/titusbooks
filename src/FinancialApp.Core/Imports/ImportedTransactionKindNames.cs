namespace FinancialApp.Core.Imports;

public static class ImportedTransactionKindNames
{
    public static string ToStorageValue(ImportedTransactionKind kind)
    {
        return kind switch
        {
            ImportedTransactionKind.Other => "other",
            ImportedTransactionKind.Payment => "payment",
            ImportedTransactionKind.Fee => "fee",
            ImportedTransactionKind.Refund => "refund",
            ImportedTransactionKind.Transfer => "transfer",
            ImportedTransactionKind.CurrencyConversion => "currency_conversion",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static ImportedTransactionKind Parse(string value)
    {
        return value switch
        {
            "other" => ImportedTransactionKind.Other,
            "payment" => ImportedTransactionKind.Payment,
            "fee" => ImportedTransactionKind.Fee,
            "refund" => ImportedTransactionKind.Refund,
            "transfer" => ImportedTransactionKind.Transfer,
            "currency_conversion" => ImportedTransactionKind.CurrencyConversion,
            _ => throw new InvalidOperationException(
                $"Unknown imported transaction kind '{value}'.")
        };
    }
}
