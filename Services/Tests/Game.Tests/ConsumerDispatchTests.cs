using System.Text.Json;
using Common.Enums;
using Common.Messaging;
using Game.Consumers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Game.Tests;

/// <summary>
/// The switch that turns a message type into a handler call. A case wired to the wrong method
/// would execute a sale as a purchase against the market maker, which compiles cleanly.
/// </summary>
public class ConsumerDispatchTests
{
    /// <summary>Exposes the protected dispatch. Constructing a consumer opens no connection.</summary>
    private sealed class TestableGameCommandConsumer()
        : GameCommandConsumer(
            NullLogger<GameCommandConsumer>.Instance,
            Options.Create(new RabbitMqOptions()),
            Substitute.For<IServiceScopeFactory>()
        )
    {
        public Task Dispatch(MessageEnvelope envelope, IServiceProvider services) =>
            HandleAsync(envelope, services, CancellationToken.None);
    }

    private readonly IGameCommandHandler _handler = Substitute.For<IGameCommandHandler>();
    private readonly IServiceProvider _services;

    public ConsumerDispatchTests() =>
        _services = new ServiceCollection().AddSingleton(_handler).BuildServiceProvider();

    [Fact]
    public async Task A_placed_bet_reaches_the_bet_handler_with_its_payload()
    {
        var evt = new BetPlacedEvent
        {
            EventId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Stake = 12.5m,
            Outcome = MarketOutcome.No,
        };

        await new TestableGameCommandConsumer().Dispatch(
            MessageEnvelope.Create(MessageTypes.BetPlaced, evt),
            _services
        );

        await _handler
            .Received(1)
            .ProcessBetPlacedAsync(
                Arg.Is<BetPlacedEvent>(e =>
                    e.EventId == evt.EventId && e.Stake == 12.5m && e.Outcome == MarketOutcome.No
                )
            );
        await _handler.DidNotReceive().ProcessSellSharesAsync(Arg.Any<SellSharesEvent>());
    }

    [Fact]
    public async Task A_share_sale_reaches_the_sale_handler_with_its_payload()
    {
        var evt = new SellSharesEvent
        {
            EventId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Shares = 4m,
            Outcome = MarketOutcome.Yes,
        };

        await new TestableGameCommandConsumer().Dispatch(
            MessageEnvelope.Create(MessageTypes.SellShares, evt),
            _services
        );

        await _handler
            .Received(1)
            .ProcessSellSharesAsync(
                Arg.Is<SellSharesEvent>(e => e.EventId == evt.EventId && e.Shares == 4m)
            );
        await _handler.DidNotReceive().ProcessBetPlacedAsync(Arg.Any<BetPlacedEvent>());
    }

    [Theory]
    [InlineData(MessageTypes.BetApproved)] // a result, not a command - wrong queue entirely
    [InlineData(MessageTypes.EventResolved)]
    [InlineData("something-invented")]
    public async Task An_unrecognised_command_type_is_rejected_rather_than_ignored(string type)
    {
        await Assert.ThrowsAsync<JsonException>(() =>
            new TestableGameCommandConsumer().Dispatch(
                new MessageEnvelope(Guid.NewGuid(), type, "{}"),
                _services
            )
        );

        await _handler.DidNotReceive().ProcessBetPlacedAsync(Arg.Any<BetPlacedEvent>());
        await _handler.DidNotReceive().ProcessSellSharesAsync(Arg.Any<SellSharesEvent>());
    }
}
