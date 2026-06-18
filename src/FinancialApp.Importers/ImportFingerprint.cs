using System.Security.Cryptography;
using System.Text;

namespace FinancialApp.Importers;

public static class ImportFingerprint
{
    public static string Create(
        string source,
        DateOnly postedDate,
        decimal amount,
        string description)
    {
        var normalizedDescription = string.Join(
            ' ',
            description.Trim().ToUpperInvariant().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
        var value = $"{source.Trim().ToUpperInvariant()}|{postedDate:yyyy-MM-dd}|{amount:0.00}|{normalizedDescription}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
