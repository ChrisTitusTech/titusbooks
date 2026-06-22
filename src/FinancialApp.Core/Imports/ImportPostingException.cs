namespace FinancialApp.Core.Imports;

public sealed class ImportPostingException : Exception
{
    public ImportPostingException(string message)
        : base(message)
    {
    }
}
