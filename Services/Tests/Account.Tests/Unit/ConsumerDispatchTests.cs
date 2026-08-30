using System.Text.Json;
using Account.Consumers;
using Common.Enums;
using Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Account.Tests.Unit;

/// <summary>
/// The switch that turns a message type into a handler call. It is a string-to-method lookup
/// spread across services, so a stale constant or a case wired to the wrong method compiles
/// cleanly and only misbehaves once a real message arrives.
/// </summary>
public class ConsumerDispatchTests
{
    private static IOptions<RabbitMqOptions> Options() =>
        Microsoft.Extensions.Options.Options.Create(new RabbitMqOptions());

    /// <summary>Exposes the protected dispatch. Constructing a consumer opens no connection.</summary>
    private sealed class TestableCommandApprovedConsumer()
        : CommandApprovedConsumer(
            NullLogger<CommandApprovedConsumer>.Instance,
            Substitute.For<IServiceScopeFactory>(),
            Options()
        )
    {
        public Task Dispatch(MessageEnvelope envelope, IServiceProvider services) =>
            HandleAsync(envelope, services, CancellationToken.None);
    }

    private sealed class TestableEventResolvedConsumer()
        : EventResolvedEventConsumer(
            NullLogger<EventResolvedEventConsumer>.Instance,
            Substitute.For<IServiceScopeFactory>(),
            Options()
        )
    {
        public Task Dispatch(MessageEnvelope envelope, IServiceProvider services) =>
            HandleAsync(envelope, services, CancellationToken.None);
    }

    private readonly ICommandApprovedHandler _commands = Substitute.For<ICommandApprovedHandler>();
    private readonly IEventResolvedHandler _resolutions = Substitute.For<IEventResolvedHandler>();
    private readonly IServiceProvider _services;

    public ConsumerDispatchTests() =>
        _services = new ServiceCollection()
            .AddSingleton(_commands)
            .AddSingleton(_resolutions)
            .BuildServiceProvider();

    [Fact]
    public async Task An_approved_bet_reaches_the_bet_handler_with_its_payload()
    {
        var evt = new BetApprovedEvent
        {
            AccId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Stake = 10m,
            ShareAmount = 19m,
            IsApproved = true,
            Outcome = MarketOutcome.No,
        };

        await new TestableCommandApprovedConsumer().Dispatch(
            MessageEnvelope.Create(MessageTypes.BetApproved, evt),
            _services
        );

        await _commands
            .Received(1)
            .ProcessBetAsync(
                Arg.Is<BetApprovedEvent>(e =>
                    e.AccId == evt.AccId && e.Stake == 10m && e.Outcome == MarketOutcome.No
                ),
                Arg.Any<CancellationToken>()
            );
        await _commands
            .DidNotReceive()
            .ProcessSaleAsync(Arg.Any<SellSharesApprovedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_approved_sale_reaches_the_sale_handler_with_its_payload()
    {
        var evt = new SellSharesApprovedEvent
        {
            AccId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            SellPrice = 10m,
            ShareAmount = 20m,
            IsApproved = true,
            Outcome = MarketOutcome.Yes,
        };

        await new TestableCommandApprovedConsumer().Dispatch(
            MessageEnvelope.Create(MessageTypes.SellSharesApproved, evt),
            _services
        );

        await _commands
            .Received(1)
            .ProcessSaleAsync(
                Arg.Is<SellSharesApprovedEvent>(e => e.AccId == evt.AccId && e.SellPrice == 10m),
                Arg.Any<CancellationToken>()
            );
        await _commands
            .DidNotReceive()
            .ProcessBetAsync(Arg.Any<BetApprovedEvent>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(MessageTypes.BetPlaced)] // a command, not a result - wrong queue entirely
    [InlineData(MessageTypes.PriceUpdate)]
    [InlineData("something-invented")]
    public async Task An_unrecognised_result_type_is_rejected_rather_than_ignored(string type)
    {
        // JsonException is the signal the base consumer treats as poison: discard, do not
        // requeue. Silently ignoring an unknown type would drop money movements without trace.
        await Assert.ThrowsAsync<JsonException>(() =>
            new TestableCommandApprovedConsumer().Dispatch(
                new MessageEnvelope(Guid.NewGuid(), type, "{}"),
                _services
            )
        );
    }

    [Fact]
    public async Task A_resolved_event_reaches_the_settlement_handler()
    {
        var evt = new EventResolvedEvent { EventId = Guid.NewGuid(), Outcome = MarketOutcome.No };

        await new TestableEventResolvedConsumer().Dispatch(
            MessageEnvelope.Create(MessageTypes.EventResolved, evt),
            _services
        );

        await _resolutions
            .Received(1)
            .ProcessEventResolvedAsync(
                Arg.Is<EventResolvedEvent>(e =>
                    e.EventId == evt.EventId && e.Outcome == MarketOutcome.No
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData(MessageTypes.BetApproved)]
    [InlineData("something-invented")]
    public async Task The_settlement_queue_rejects_anything_but_a_resolution(string type)
    {
        await Assert.ThrowsAsync<JsonException>(() =>
            new TestableEventResolvedConsumer().Dispatch(
                new MessageEnvelope(Guid.NewGuid(), type, "{}"),
                _services
            )
        );

        await _resolutions
            .DidNotReceive()
            .ProcessEventResolvedAsync(Arg.Any<EventResolvedEvent>(), Arg.Any<CancellationToken>());
    }
}
