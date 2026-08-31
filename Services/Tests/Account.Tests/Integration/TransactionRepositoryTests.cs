using Account.DTOs;
using Common.Enums;
using Eventus.Testing;

namespace Account.Tests.Integration;

public class TransactionRepositoryTests(PostgresFixture postgres) : AccountDatabaseTest(postgres)
{
    private static TransactionDTO Holding(
        Guid eventId,
        Guid userId,
        decimal shares = 5m,
        MarketOutcome outcome = MarketOutcome.Yes,
        TransactionType type = TransactionType.Buy
    ) => new(eventId, userId, shares, outcome, type);

    [Fact]
    public async Task A_transaction_is_persisted_with_a_generated_id()
    {
        var user = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        Assert.True(await Transactions.CreateTransaction(Holding(eventId, user, 7.5m)));

        var stored = Assert.Single(await StoredTransactions(user));
        Assert.NotEqual(Guid.Empty, stored.TransactionId);
        Assert.Equal(eventId, stored.EventId);
        Assert.Equal(user, stored.AccountId);
        Assert.Equal(7.5m, stored.ShareAmount);
        Assert.Equal(MarketOutcome.Yes, stored.Outcome);
        Assert.Equal(TransactionType.Buy, stored.Type);
    }

    [Fact]
    public async Task Sales_are_recorded_with_their_own_type()
    {
        var user = Guid.NewGuid();

        await Transactions.CreateTransaction(
            Holding(Guid.NewGuid(), user, type: TransactionType.Sell)
        );

        Assert.Equal(TransactionType.Sell, Assert.Single(await StoredTransactions(user)).Type);
    }

    [Fact]
    public async Task Transactions_are_read_back_for_one_user_only()
    {
        var user = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await Transactions.CreateTransaction(Holding(eventId, user, 3m));
        await Transactions.CreateTransaction(Holding(eventId, Guid.NewGuid(), 9m));

        var mine = await Transactions.GetTransactionsForUser(user);

        var only = Assert.Single(mine);
        // The entity calls it AccountId and the DTO calls it UserId; the profile bridges them,
        // and a silent failure here would hand every caller an empty Guid.
        Assert.Equal(user, only.UserId);
        Assert.Equal(3m, only.ShareAmount);
    }

    [Fact]
    public async Task Transactions_are_read_back_for_one_event_only()
    {
        var eventId = Guid.NewGuid();
        await Transactions.CreateTransaction(Holding(eventId, Guid.NewGuid()));
        await Transactions.CreateTransaction(Holding(eventId, Guid.NewGuid()));
        await Transactions.CreateTransaction(Holding(Guid.NewGuid(), Guid.NewGuid()));

        var forEvent = await Transactions.GetTransactionsForEvent(eventId);

        Assert.Equal(2, forEvent.Count);
        Assert.All(forEvent, t => Assert.Equal(eventId, t.EventId));
    }

    [Fact]
    public async Task An_unknown_user_has_no_transactions()
    {
        Assert.Empty(await Transactions.GetTransactionsForUser(Guid.NewGuid()));
    }

    [Fact]
    public async Task Share_precision_survives_a_round_trip()
    {
        var user = Guid.NewGuid();
        var shares = 11m - 1m / 11m;

        await Transactions.CreateTransaction(Holding(Guid.NewGuid(), user, shares));

        Assert.Equal(
            shares,
            Assert.Single(await Transactions.GetTransactionsForUser(user)).ShareAmount
        );
    }
}
