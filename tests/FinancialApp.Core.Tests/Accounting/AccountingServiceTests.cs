using FinancialApp.Core.Accounting;

namespace FinancialApp.Core.Tests.Accounting;

public sealed class AccountingServiceTests
{
    private readonly Guid organizationId = Guid.NewGuid();
    private readonly Guid checkingAccountId = Guid.NewGuid();
    private readonly Guid expenseAccountId = Guid.NewGuid();

    [Fact]
    public async Task PostJournalEntryAsync_RejectsUnbalancedEntry()
    {
        var repository = new InMemoryJournalEntryRepository();
        var service = new AccountingService(repository);

        var draft = new JournalEntryDraft
        {
            OrganizationId = organizationId,
            EntryDate = new DateOnly(2026, 6, 16),
            Memo = "Unbalanced expense",
            Lines =
            [
                new JournalLineDraft { AccountId = expenseAccountId, Debit = 42.00m },
                new JournalLineDraft { AccountId = checkingAccountId, Credit = 41.00m }
            ]
        };

        var exception = await Assert.ThrowsAsync<AccountingException>(() => service.PostJournalEntryAsync(draft));

        Assert.Contains("unbalanced", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(repository.Entries);
    }

    [Fact]
    public async Task PostJournalEntryAsync_PersistsBalancedEntry()
    {
        var repository = new InMemoryJournalEntryRepository();
        var service = new AccountingService(repository);

        var draft = new JournalEntryDraft
        {
            OrganizationId = organizationId,
            EntryDate = new DateOnly(2026, 6, 16),
            Memo = "Office supplies",
            Lines =
            [
                new JournalLineDraft { AccountId = expenseAccountId, Debit = 42.00m },
                new JournalLineDraft { AccountId = checkingAccountId, Credit = 42.00m }
            ]
        };

        var entry = await service.PostJournalEntryAsync(draft);

        Assert.True(entry.IsBalanced);
        Assert.Single(repository.Entries);
        Assert.Equal(entry.Id, repository.Entries[0].Id);
        Assert.All(entry.Lines, line => Assert.Equal(entry.Id, line.JournalEntryId));
    }

    [Fact]
    public async Task PostJournalEntryAsync_RejectsLineWithDebitAndCredit()
    {
        var repository = new InMemoryJournalEntryRepository();
        var service = new AccountingService(repository);

        var draft = new JournalEntryDraft
        {
            OrganizationId = organizationId,
            EntryDate = new DateOnly(2026, 6, 16),
            Lines =
            [
                new JournalLineDraft { AccountId = expenseAccountId, Debit = 42.00m, Credit = 1.00m },
                new JournalLineDraft { AccountId = checkingAccountId, Credit = 42.00m }
            ]
        };

        await Assert.ThrowsAsync<AccountingException>(() => service.PostJournalEntryAsync(draft));

        Assert.Empty(repository.Entries);
    }

    private sealed class InMemoryJournalEntryRepository : IJournalEntryRepository
    {
        public List<JournalEntry> Entries { get; } = [];

        public Task AddAsync(JournalEntry journalEntry, CancellationToken cancellationToken = default)
        {
            Entries.Add(journalEntry);
            return Task.CompletedTask;
        }
    }
}
