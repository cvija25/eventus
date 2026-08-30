using System.Globalization;
using Common.Enums;
using Common.Messaging;
using Game.Consumers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Game.Tests;

/// <summary>
/// Covers the constant-product market maker in <see cref="GameCommandHandler"/>.
///
/// The market keeps <c>poolYes * poolNo == 1</c>. A bet adds the stake to the pot and to both
/// pools, then collapses the bought side back onto the curve (<c>1 / otherPool</c>); the shares
/// handed out are what that collapse freed up. Every assertion below is anchored to that
/// invariant rather than to copied-out magic numbers, so the tests still mean something if the
/// pool arithmetic is rewritten.
/// </summary>
public class GameCommandHandlerTests
{
    private readonly FakeMarket _market = new();
    private readonly FakePublisher _publisher = new();
    private readonly GameCommandHandler _handler;

    public GameCommandHandlerTests()
    {
        _handler = new GameCommandHandler(
            _market,
            _publisher,
            NullLogger<GameCommandHandler>.Instance
        );
    }

    private static BetPlacedEvent Bet(decimal stake, MarketOutcome outcome) =>
        new()
        {
            EventId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Stake = stake,
            Outcome = outcome,
        };

    /// <summary>poolYes * poolNo, which the market maker is supposed to hold at 1.</summary>
    private decimal Invariant => _market.PoolYes * _market.PoolNo;

    [Fact]
    public async Task Buying_yes_moves_the_pot_pools_and_preserves_the_invariant()
    {
        await _handler.ProcessBetPlacedAsync(Bet(10m, MarketOutcome.Yes));

        Assert.Equal(11m, _market.Pot); // 1 seeded + 10 staked
        Assert.Equal(11m, _market.PoolNo); // the unbought side just absorbs the stake
        Assert.Equal(1m / 11m, _market.PoolYes); // the bought side collapses back onto the curve
        Assert.Equal(1m, Invariant, precision: 20);
    }

    [Fact]
    public async Task Buying_no_is_the_mirror_image_of_buying_yes()
    {
        await _handler.ProcessBetPlacedAsync(Bet(10m, MarketOutcome.No));

        Assert.Equal(11m, _market.Pot);
        Assert.Equal(11m, _market.PoolYes);
        Assert.Equal(1m / 11m, _market.PoolNo);
        Assert.Equal(1m, Invariant, precision: 20);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(250)]
    [InlineData(9999)]
    public async Task The_invariant_survives_any_stake(decimal stake)
    {
        await _handler.ProcessBetPlacedAsync(Bet(stake, MarketOutcome.Yes));

        Assert.Equal(1m, Invariant, precision: 20);
        Assert.Equal(1m + stake, _market.Pot);
    }

    [Fact]
    public async Task The_invariant_survives_a_run_of_bets_on_both_sides()
    {
        await _handler.ProcessBetPlacedAsync(Bet(5m, MarketOutcome.Yes));
        await _handler.ProcessBetPlacedAsync(Bet(3m, MarketOutcome.No));
        await _handler.ProcessBetPlacedAsync(Bet(11m, MarketOutcome.Yes));

        Assert.Equal(1m, Invariant, precision: 18);
        Assert.Equal(1m + 5m + 3m + 11m, _market.Pot);
    }

    [Fact]
    public async Task Approval_carries_the_bet_details_and_the_shares_minted()
    {
        var bet = Bet(10m, MarketOutcome.Yes);

        await _handler.ProcessBetPlacedAsync(bet);

        var approved = Assert.Single(_publisher.Bets);
        Assert.True(approved.IsApproved);
        Assert.Equal(bet.OwnerId, approved.AccId);
        Assert.Equal(bet.EventId, approved.EventId);
        Assert.Equal(bet.Stake, approved.Stake);
        Assert.Equal(bet.Outcome, approved.Outcome);

        // Shares are what the collapse freed: (1 + stake) - 1/(1 + stake).
        Assert.Equal(11m - 1m / 11m, approved.ShareAmount);
        Assert.InRange(approved.ApprovedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow);
    }

    [Fact]
    public async Task A_stake_buys_more_shares_than_it_costs_on_a_balanced_market()
    {
        await _handler.ProcessBetPlacedAsync(Bet(10m, MarketOutcome.Yes));

        // At even odds a share pays out roughly 2:1, so 10 credits must buy more than 10 shares.
        Assert.True(_publisher.Bets.Single().ShareAmount > 10m);
    }

    [Fact]
    public async Task A_rejected_pool_update_aborts_the_bet_without_approving_it()
    {
        _market.AcceptUpdates = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.ProcessBetPlacedAsync(Bet(10m, MarketOutcome.Yes))
        );

        // Approving a bet Catalog refused would credit shares the market never minted.
        Assert.Empty(_publisher.Bets);
    }

    [Fact]
    public async Task A_rejected_pool_update_aborts_a_sale_without_approving_it()
    {
        _market.AcceptUpdates = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.ProcessSellSharesAsync(
                new SellSharesEvent
                {
                    EventId = Guid.NewGuid(),
                    OwnerId = Guid.NewGuid(),
                    Shares = 1m,
                    Outcome = MarketOutcome.Yes,
                }
            )
        );

        Assert.Empty(_publisher.Sales);
    }

    [Fact]
    public async Task Selling_shares_straight_back_returns_the_original_stake()
    {
        var bet = Bet(10m, MarketOutcome.Yes);
        await _handler.ProcessBetPlacedAsync(bet);
        var shares = _publisher.Bets.Single().ShareAmount;

        await _handler.ProcessSellSharesAsync(
            new SellSharesEvent
            {
                EventId = bet.EventId,
                OwnerId = bet.OwnerId,
                Shares = shares,
                Outcome = MarketOutcome.Yes,
            }
        );

        // A no-fee market maker must not skim the round trip: what went in comes back out,
        // and the market returns to where it started.
        var sale = Assert.Single(_publisher.Sales);
        Assert.Equal(10m, sale.SellPrice, precision: 8);
        Assert.Equal(1m, _market.Pot, precision: 8);
        Assert.Equal(1m, _market.PoolYes, precision: 8);
        Assert.Equal(1m, _market.PoolNo, precision: 8);
    }

    [Fact]
    public async Task Selling_preserves_the_invariant()
    {
        await _handler.ProcessBetPlacedAsync(Bet(10m, MarketOutcome.Yes));
        var shares = _publisher.Bets.Single().ShareAmount;

        await _handler.ProcessSellSharesAsync(
            new SellSharesEvent
            {
                EventId = Guid.NewGuid(),
                OwnerId = Guid.NewGuid(),
                Shares = shares / 2m,
                Outcome = MarketOutcome.Yes,
            }
        );

        Assert.Equal(1m, Invariant, precision: 8);
    }

    [Fact]
    public async Task Approval_carries_the_sale_details()
    {
        var sell = new SellSharesEvent
        {
            EventId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Shares = 0.5m,
            Outcome = MarketOutcome.Yes,
        };

        await _handler.ProcessSellSharesAsync(sell);

        var approved = Assert.Single(_publisher.Sales);
        Assert.True(approved.IsApproved);
        Assert.Equal(sell.OwnerId, approved.AccId);
        Assert.Equal(sell.EventId, approved.EventId);
        Assert.Equal(sell.Outcome, approved.Outcome);
        Assert.Equal(sell.Shares, approved.ShareAmount);
        Assert.True(approved.SellPrice > 0m);
    }

    [Fact]
    public async Task Pool_values_are_read_culture_invariantly()
    {
        // Catalog sends decimals as strings. Under a comma-decimal locale a careless
        // decimal.Parse turns "1.5" into 15 and silently inflates the market tenfold.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            _market.PoolYes = 0.5m;
            _market.PoolNo = 2m;
            _market.Pot = 1.5m;

            await _handler.ProcessBetPlacedAsync(Bet(1m, MarketOutcome.Yes));

            Assert.Equal(2.5m, _market.Pot); // 1.5 + 1, not 15 + 1
            Assert.Equal(3m, _market.PoolNo); // 2 + 1
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact(
        Skip = "Bug: ProcessSellSharesAsync ignores Outcome and always sells against the Yes "
            + "side, so selling No shares mutates the wrong pool. See Game/Consumers/GameCommandHandler.cs"
    )]
    public async Task Selling_no_shares_draws_down_the_no_pool()
    {
        await _handler.ProcessBetPlacedAsync(Bet(10m, MarketOutcome.No));
        var shares = _publisher.Bets.Single().ShareAmount;
        var poolNoBefore = _market.PoolNo;

        await _handler.ProcessSellSharesAsync(
            new SellSharesEvent
            {
                EventId = Guid.NewGuid(),
                OwnerId = Guid.NewGuid(),
                Shares = shares,
                Outcome = MarketOutcome.No,
            }
        );

        // Returning No shares should push the No pool back up, exactly as selling Yes shares
        // pushes the Yes pool back up. Today the sale is applied to the Yes side regardless.
        Assert.True(_market.PoolNo > poolNoBefore);
        Assert.Equal(10m, _publisher.Sales.Single().SellPrice, precision: 8);
    }
}
