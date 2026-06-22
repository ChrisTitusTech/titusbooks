namespace FinancialApp.Core.Reconciliation;

public sealed class ReconciliationException : Exception
{
    public ReconciliationException(string message)
        : base(message)
    {
    }
}
