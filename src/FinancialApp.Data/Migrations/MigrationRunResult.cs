namespace FinancialApp.Data.Migrations;

public sealed record MigrationRunResult(bool Succeeded, string? ErrorMessage)
{
    public static MigrationRunResult Success() => new(true, null);

    public static MigrationRunResult Failure(Exception? exception)
    {
        return new(
            false,
            exception is null
                ? "Database migration failed."
                : $"Database migration failed with {exception.GetType().Name}.");
    }
}
