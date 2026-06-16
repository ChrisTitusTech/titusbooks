namespace FinancialApp.Core.Accounting;

public sealed class AccountingException : InvalidOperationException
{
    public AccountingException(string message)
        : base(message)
    {
    }
}
