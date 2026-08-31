using Account.Controllers;
using Account.DTOs;
using Account.Publishers;
using Account.Repositories;
using Common.Enums;
using Common.Messaging;
using Common.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Account.Tests.Unit;

public class AccountControllerTests
{
    private readonly IGameCommandPublisher _publisher = Substitute.For<IGameCommandPublisher>();
    private readonly IWalletRepository _wallets = Substitute.For<IWalletRepository>();
    private readonly ITransactionRepository _transactions =
        Substitute.For<ITransactionRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly AccountController _controller;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();

    public AccountControllerTests()
    {
        _currentUser.UserId.Returns(_userId);
        _transactions.GetTransactionsForUser(Arg.Any<Guid>()).Returns([]);
        _controller = new AccountController(
            _publisher,
            _wallets,
            _transactions,
            _currentUser,
            NullLogger<AccountController>.Instance
        );
    }

    private void WalletHolds(decimal amount) =>
        _wallets.GetBalance(_userId).Returns(new WalletBalanceDto(amount));

    private void UserOwns(decimal shares, MarketOutcome outcome, Guid? eventId = null) =>
        _transactions
            .GetTransactionsForUser(_userId)
            .Returns([
                new TransactionDTO(
                    eventId ?? _eventId,
                    _userId,
                    shares,
                    outcome,
                    TransactionType.Buy
                ),
            ]);

    private Task<ActionResult<string>> Buy(
        decimal stake,
        MarketOutcome outcome = MarketOutcome.Yes
    ) =>
        _controller.PublishBet(
            new BetPlacedRequest
            {
                Stake = stake,
                EventId = _eventId,
                Outcome = outcome,
            }
        );

    private Task<ActionResult<string>> Sell(
        decimal shares,
        MarketOutcome outcome = MarketOutcome.Yes
    ) =>
        _controller.PublishSellShares(
            new SellSharesRequest
            {
                Shares = shares,
                EventId = _eventId,
                Outcome = outcome,
            }
        );

    private async Task AssertNothingHappened()
    {
        await _publisher.DidNotReceive().PublishBetPlacedAsync(Arg.Any<BetPlacedEvent>());
        await _publisher.DidNotReceive().PublishSellSharesAsync(Arg.Any<SellSharesEvent>());
        await _wallets.DidNotReceive().DepositReserveFund(Arg.Any<Guid>(), Arg.Any<decimal>());
    }

    // ---- buy -------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public async Task Buying_a_non_positive_stake_is_rejected(decimal stake)
    {
        WalletHolds(100m);

        Assert.IsType<BadRequestObjectResult>((await Buy(stake)).Result);
        await AssertNothingHappened();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Buying_an_outcome_outside_the_enum_is_rejected(int outcome)
    {
        WalletHolds(100m);

        Assert.IsType<BadRequestObjectResult>((await Buy(10m, (MarketOutcome)outcome)).Result);
        await AssertNothingHappened();
    }

    [Fact]
    public async Task Buying_without_a_caller_is_401()
    {
        _currentUser.UserId.Returns((Guid?)null);

        Assert.IsType<UnauthorizedResult>((await Buy(10m)).Result);
        await AssertNothingHappened();
    }

    [Fact]
    public async Task Buying_beyond_the_balance_is_a_conflict()
    {
        WalletHolds(5m);

        Assert.IsType<ConflictObjectResult>((await Buy(10m)).Result);
        // Neither the hold nor the command may happen, or the user spends money they lack.
        await AssertNothingHappened();
    }

    [Fact]
    public async Task Buying_reserves_the_stake_before_publishing_the_command()
    {
        WalletHolds(100m);

        Assert.IsType<OkObjectResult>((await Buy(10m, MarketOutcome.No)).Result);

        Received.InOrder(() =>
        {
            // The hold has to land first: the bet is settled asynchronously, and an unheld
            // balance could be spent twice before the approval comes back.
            _wallets.DepositReserveFund(_userId, 10m);
            _publisher.PublishBetPlacedAsync(
                Arg.Is<BetPlacedEvent>(e =>
                    e.OwnerId == _userId
                    && e.EventId == _eventId
                    && e.Stake == 10m
                    && e.Outcome == MarketOutcome.No
                )
            );
        });
    }

    [Fact]
    public async Task Buying_exactly_the_whole_balance_is_allowed()
    {
        WalletHolds(10m);

        Assert.IsType<OkObjectResult>((await Buy(10m)).Result);
        await _publisher.Received(1).PublishBetPlacedAsync(Arg.Any<BetPlacedEvent>());
    }

    // ---- sell ------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Selling_a_non_positive_quantity_is_rejected(decimal shares)
    {
        Assert.IsType<BadRequestObjectResult>((await Sell(shares)).Result);
        await AssertNothingHappened();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Selling_an_outcome_outside_the_enum_is_rejected(int outcome)
    {
        Assert.IsType<BadRequestObjectResult>((await Sell(1m, (MarketOutcome)outcome)).Result);
        await AssertNothingHappened();
    }

    [Fact]
    public async Task Selling_without_a_caller_is_401()
    {
        _currentUser.UserId.Returns((Guid?)null);

        Assert.IsType<UnauthorizedResult>((await Sell(1m)).Result);
        await AssertNothingHappened();
    }

    [Fact]
    public async Task Selling_more_than_is_held_is_a_conflict()
    {
        UserOwns(2m, MarketOutcome.Yes);

        Assert.IsType<ConflictObjectResult>((await Sell(5m)).Result);
        await AssertNothingHappened();
    }

    [Fact]
    public async Task Shares_held_on_the_other_outcome_do_not_count()
    {
        UserOwns(10m, MarketOutcome.No);

        Assert.IsType<ConflictObjectResult>((await Sell(5m, MarketOutcome.Yes)).Result);
        await AssertNothingHappened();
    }

    [Fact]
    public async Task Shares_held_on_another_event_do_not_count()
    {
        UserOwns(10m, MarketOutcome.Yes, eventId: Guid.NewGuid());

        Assert.IsType<ConflictObjectResult>((await Sell(5m)).Result);
        await AssertNothingHappened();
    }

    [Fact]
    public async Task Selling_what_is_held_publishes_the_command()
    {
        UserOwns(10m, MarketOutcome.Yes);

        Assert.IsType<OkObjectResult>((await Sell(4m)).Result);

        await _publisher
            .Received(1)
            .PublishSellSharesAsync(
                Arg.Is<SellSharesEvent>(e =>
                    e.OwnerId == _userId
                    && e.EventId == _eventId
                    && e.Shares == 4m
                    && e.Outcome == MarketOutcome.Yes
                )
            );
    }

    // ---- balance, transactions, deposit, wallet --------------------------------------

    [Fact]
    public async Task Balance_reports_the_spendable_amount()
    {
        WalletHolds(42.5m);

        var result = await _controller.GetBalance();

        var body = Assert.IsType<OkObjectResult>(result.Result).Value;
        Assert.Equal(42.5m, body!.GetType().GetProperty("amount")!.GetValue(body));
    }

    [Fact]
    public async Task Balance_without_a_wallet_is_404()
    {
        _wallets.GetBalance(_userId).Returns((WalletBalanceDto?)null);

        Assert.IsType<NotFoundObjectResult>((await _controller.GetBalance()).Result);
    }

    [Fact]
    public async Task Balance_without_a_caller_is_401()
    {
        _currentUser.UserId.Returns((Guid?)null);

        Assert.IsType<UnauthorizedResult>((await _controller.GetBalance()).Result);
    }

    [Fact]
    public async Task Transactions_are_listed_for_the_caller_only()
    {
        List<TransactionDTO> held =
        [
            new(_eventId, _userId, 3m, MarketOutcome.Yes, TransactionType.Buy),
        ];
        _transactions.GetTransactionsForUser(_userId).Returns(held);

        var result = await _controller.GetTransactions();

        Assert.Same(held, Assert.IsType<OkObjectResult>(result.Result).Value);
        await _transactions.Received(1).GetTransactionsForUser(_userId);
    }

    [Fact]
    public async Task Transactions_without_a_caller_is_401()
    {
        _currentUser.UserId.Returns((Guid?)null);

        Assert.IsType<UnauthorizedResult>((await _controller.GetTransactions()).Result);
    }

    [Fact]
    public async Task Deposit_credits_the_callers_own_wallet()
    {
        _wallets.Deposit(_userId, 25m).Returns(new UpdateWalletDto(_userId, 125m));

        var result = await _controller.DepositCoins(new DepositRequest(25m));

        var updated = Assert.IsType<UpdateWalletDto>(
            Assert.IsType<OkObjectResult>(result.Result).Value
        );
        Assert.Equal(125m, updated.Amount);
        await _wallets.Received(1).Deposit(_userId, 25m);
    }

    [Fact]
    public async Task Deposit_without_a_wallet_is_404()
    {
        _wallets.Deposit(_userId, 25m).Returns((UpdateWalletDto?)null);

        Assert.IsType<NotFoundObjectResult>(
            (await _controller.DepositCoins(new DepositRequest(25m))).Result
        );
    }

    [Fact]
    public async Task Deposit_without_a_caller_is_401()
    {
        _currentUser.UserId.Returns((Guid?)null);

        Assert.IsType<UnauthorizedResult>(
            (await _controller.DepositCoins(new DepositRequest(25m))).Result
        );
    }

    [Fact]
    public async Task Creating_a_wallet_delegates_to_the_repository_and_returns_no_content()
    {
        var newUser = Guid.NewGuid();

        var result = await _controller.CreateWallet(new CreateWalletDto(newUser));

        Assert.IsType<NoContentResult>(result.Result);
        await _wallets.Received(1).CreateWalletIfMissing(newUser);
    }

    [Fact]
    public async Task Buying_without_a_wallet_is_400()
    {
        _wallets.GetBalance(_userId).Returns((WalletBalanceDto?)null);

        var result = await Buy(10m);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        await AssertNothingHappened();
    }

    [Fact]
    public async Task Selling_shares_reduces_what_can_be_sold_again()
    {
        _transactions
            .GetTransactionsForUser(_userId)
            .Returns([
                new TransactionDTO(_eventId, _userId, 10m, MarketOutcome.Yes, TransactionType.Buy),
                new TransactionDTO(_eventId, _userId, 10m, MarketOutcome.Yes, TransactionType.Sell),
            ]);

        // Bought 10 and already sold 10, so nothing is left to sell.
        Assert.IsType<ConflictObjectResult>((await Sell(5m)).Result);
        await AssertNothingHappened();
    }
}
