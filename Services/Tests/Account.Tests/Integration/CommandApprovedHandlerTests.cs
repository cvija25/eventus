using Account.Consumers;
using Account.DTOs;
using Account.Repositories;
using Common.Enums;
using Common.Messaging;
using Eventus.Testing;
using NSubstitute;

namespace Account.Tests.Integration;

/// <summary>
/// The consumer that turns Game's approvals into money movements. It runs everything inside a
/// real database transaction, so these tests use Postgres rather than a fake provider.
/// </summary>
public class CommandApprovedHandlerTests(PostgresFixture postgres) : AccountDatabaseTest(postgres)
{
    private CommandApprovedHandler Handler => new(Wallets, Transactions, Context);

    private CommandApprovedHandler HandlerWith(ITransactionRepository transactions) =>
        new(Wallets, transactions, Context);

    private static BetApprovedEvent Approval(
        Guid account,
        decimal stake = 10m,
        decimal shares = 19m,
        bool approved = true
    ) =>
        new()
        {
            AccId = account,
            Stake = stake,
            ShareAmount = shares,
            IsApproved = approved,
            ApprovedAt = DateTime.UtcNow,
            EventId = Guid.NewGuid(),
            Outcome = MarketOutcome.Yes,
        };

    [Fact]
    public async Task An_approved_bet_settles_the_hold_and_records_the_shares()
    {
        var account = await GivenWallet(available: 100m, reserved: 10m);
        var evt = Approval(account, stake: 10m, shares: 19m);

        await Handler.ProcessBetAsync(evt, CancellationToken.None);

        var wallet = await StoredWallet(account);
        Assert.Equal(90m, wallet.AvailableFunds); // the stake is really spent
        Assert.Equal(0m, wallet.ReserveFunds); // and the hold is released
        var recorded = Assert.Single(await StoredTransactions(account));
        Assert.Equal(19m, recorded.ShareAmount);
        Assert.Equal(TransactionType.Buy, recorded.Type);
        Assert.Equal(evt.EventId, recorded.EventId);
    }

    [Fact]
    public async Task A_rejected_bet_releases_the_hold_and_costs_nothing()
    {
        var account = await GivenWallet(available: 100m, reserved: 10m);

        await Handler.ProcessBetAsync(Approval(account, approved: false), CancellationToken.None);

        var wallet = await StoredWallet(account);
        Assert.Equal(100m, wallet.AvailableFunds);
        Assert.Equal(0m, wallet.ReserveFunds);
        Assert.Empty(await StoredTransactions(account));
    }

    [Fact]
    public async Task A_failure_part_way_through_leaves_the_wallet_untouched()
    {
        var account = await GivenWallet(available: 100m, reserved: 10m);
        var failing = Substitute.For<ITransactionRepository>();
        failing
            .CreateTransaction(Arg.Any<TransactionDTO>())
            .Returns<Task<bool>>(_ =>
                throw new InvalidOperationException("transaction store is down")
            );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HandlerWith(failing).ProcessBetAsync(Approval(account), CancellationToken.None)
        );

        // Without the rollback the stake would be gone with no shares to show for it.
        var wallet = await StoredWallet(account);
        Assert.Equal(100m, wallet.AvailableFunds);
        Assert.Equal(10m, wallet.ReserveFunds);
        Assert.Empty(await StoredTransactions(account));
    }

    [Fact]
    public async Task An_approved_sale_is_recorded_against_the_event()
    {
        var account = await GivenWallet(available: 100m);
        var evt = new SellSharesApprovedEvent
        {
            AccId = account,
            SellPrice = 10m,
            ShareAmount = 20m,
            IsApproved = true,
            ApprovedAt = DateTime.UtcNow,
            EventId = Guid.NewGuid(),
            Outcome = MarketOutcome.No,
        };

        await Handler.ProcessSaleAsync(evt, CancellationToken.None);

        var recorded = Assert.Single(await StoredTransactions(account));
        Assert.Equal(TransactionType.Sell, recorded.Type);
        Assert.Equal(20m, recorded.ShareAmount);
        Assert.Equal(MarketOutcome.No, recorded.Outcome);
    }

    [Fact]
    public async Task A_rejected_sale_does_nothing_at_all()
    {
        var account = await GivenWallet(available: 100m);

        await Handler.ProcessSaleAsync(
            new SellSharesApprovedEvent
            {
                AccId = account,
                SellPrice = 10m,
                ShareAmount = 20m,
                IsApproved = false,
                EventId = Guid.NewGuid(),
                Outcome = MarketOutcome.Yes,
            },
            CancellationToken.None
        );

        Assert.Equal(100m, (await StoredWallet(account)).AvailableFunds);
        Assert.Empty(await StoredTransactions(account));
    }

    [Fact]
    public async Task An_approved_sale_credits_the_payout_game_calculated()
    {
        var account = await GivenWallet(available: 100m);

        await Handler.ProcessSaleAsync(
            new SellSharesApprovedEvent
            {
                AccId = account,
                SellPrice = 10m, // Game's CalculatePayout returns the payout for the whole lot
                ShareAmount = 20m,
                IsApproved = true,
                EventId = Guid.NewGuid(),
                Outcome = MarketOutcome.Yes,
            },
            CancellationToken.None
        );

        Assert.Equal(110m, (await StoredWallet(account)).AvailableFunds);
    }
}
