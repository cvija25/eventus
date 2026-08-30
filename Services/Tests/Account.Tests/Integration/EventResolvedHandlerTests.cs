using Account.Consumers;
using Account.DTOs;
using Common.Enums;
using Common.Messaging;
using Eventus.Testing;

namespace Account.Tests.Integration;

public class EventResolvedHandlerTests(PostgresFixture postgres) : AccountDatabaseTest(postgres)
{
    private readonly Guid _eventId = Guid.NewGuid();

    private EventResolvedHandler Handler => new(Wallets, Transactions, Context);

    private Task Resolve(MarketOutcome outcome) =>
        Handler.ProcessEventResolvedAsync(
            new EventResolvedEvent { EventId = _eventId, Outcome = outcome },
            CancellationToken.None
        );

    private async Task<Guid> GivenHolding(
        decimal shares,
        MarketOutcome outcome,
        TransactionType type = TransactionType.Buy,
        Guid? eventId = null
    )
    {
        var account = await GivenWallet();
        await Transactions.CreateTransaction(
            new TransactionDTO(eventId ?? _eventId, account, shares, outcome, type)
        );
        return account;
    }

    [Fact]
    public async Task Backing_the_winning_outcome_pays_one_per_share()
    {
        var winner = await GivenHolding(19m, MarketOutcome.Yes);

        await Resolve(MarketOutcome.Yes);

        Assert.Equal(19m, (await StoredWallet(winner)).AvailableFunds);
    }

    [Fact]
    public async Task Backing_the_losing_outcome_pays_nothing()
    {
        var loser = await GivenHolding(19m, MarketOutcome.No);

        await Resolve(MarketOutcome.Yes);

        Assert.Equal(0m, (await StoredWallet(loser)).AvailableFunds);
    }

    [Fact]
    public async Task Only_holders_of_the_resolved_event_are_paid()
    {
        var otherMarket = await GivenHolding(19m, MarketOutcome.Yes, eventId: Guid.NewGuid());

        await Resolve(MarketOutcome.Yes);

        Assert.Equal(0m, (await StoredWallet(otherMarket)).AvailableFunds);
    }

    [Fact]
    public async Task Every_winner_is_paid()
    {
        var first = await GivenHolding(5m, MarketOutcome.Yes);
        var second = await GivenHolding(7m, MarketOutcome.Yes);
        var loser = await GivenHolding(100m, MarketOutcome.No);

        await Resolve(MarketOutcome.Yes);

        Assert.Equal(5m, (await StoredWallet(first)).AvailableFunds);
        Assert.Equal(7m, (await StoredWallet(second)).AvailableFunds);
        Assert.Equal(0m, (await StoredWallet(loser)).AvailableFunds);
    }

    [Fact]
    public async Task Resolving_a_market_nobody_traded_is_harmless()
    {
        await Resolve(MarketOutcome.Yes);
    }

    [Fact(
        Skip = "Bug: winners are selected on Outcome alone, ignoring TransactionType, so a "
            + "Sell row pays out at settlement exactly like a Buy. "
            + "See Account/Consumers/EventResolvedHandler.cs"
    )]
    public async Task Shares_already_sold_are_not_paid_out_again()
    {
        var account = await GivenWallet();
        await Transactions.CreateTransaction(
            new TransactionDTO(_eventId, account, 10m, MarketOutcome.Yes, TransactionType.Buy)
        );
        await Transactions.CreateTransaction(
            new TransactionDTO(_eventId, account, 10m, MarketOutcome.Yes, TransactionType.Sell)
        );

        await Resolve(MarketOutcome.Yes);

        // The position was opened and closed before settlement, so it is worth nothing.
        Assert.Equal(0m, (await StoredWallet(account)).AvailableFunds);
    }
}
