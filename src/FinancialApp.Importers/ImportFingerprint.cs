using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FinancialApp.Importers;

public static class ImportFingerprint
{
    public static string Create(
        string source,
        DateOnly postedDate,
        decimal amount,
        string description,
        string? sourceTransactionId = null)
    {
        var normalizedSource = source.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(sourceTransactionId))
        {
            return Hash($"{normalizedSource}|ID|{sourceTransactionId.Trim().ToUpperInvariant()}");
        }

        var normalizedDescription = string.Join(
            ' ',
            description.Trim().ToUpperInvariant().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
        var value = string.Create(
            CultureInfo.InvariantCulture,
            $"{normalizedSource}|{postedDate:yyyy-MM-dd}|{amount:0.00}|{normalizedDescription}");
        return Hash(value);
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
