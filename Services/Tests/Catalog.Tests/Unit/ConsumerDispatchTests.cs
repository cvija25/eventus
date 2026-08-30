using System.Text.Json;
using Catalog.API.Consumers;
using Common.Messaging;
using Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Catalog.Tests.Unit;

public class ConsumerDispatchTests
{
    /// <summary>Exposes the protected dispatch. Constructing a consumer opens no connection.</summary>
    private sealed class TestablePriceUpdateConsumer()
        : PriceUpdateConsumer(
            NullLogger<PriceUpdateConsumer>.Instance,
            Substitute.For<IServiceScopeFactory>(),
            Options.Create(
                new KafkaOptions
                {
                    BootstrapServers = "unused:9092",
                    PriceUpdateTopic = KafkaConstants.PriceUpdateTopic,
                }
            )
        )
    {
        public Task Dispatch(MessageEnvelope envelope, IServiceProvider services) =>
            HandleAsync(envelope, services, CancellationToken.None);
    }

    private readonly IPriceUpdateHandler _handler = Substitute.For<IPriceUpdateHandler>();
    private readonly IServiceProvider _services;

    public ConsumerDispatchTests() =>
        _services = new ServiceCollection().AddSingleton(_handler).BuildServiceProvider();

    [Fact]
    public async Task A_price_update_reaches_the_handler_with_its_payload()
    {
        var evt = new PriceUpdateEvent
        {
            EventId = Guid.NewGuid(),
            PriceYes = 0.75m,
            PriceNo = 0.25m,
        };

        await new TestablePriceUpdateConsumer().Dispatch(
            MessageEnvelope.Create(MessageTypes.PriceUpdate, evt),
            _services
        );

        _handler
            .Received(1)
            .ProcessPriceUpdateAsync(
                Arg.Is<PriceUpdateEvent>(e =>
                    e.EventId == evt.EventId && e.PriceYes == 0.75m && e.PriceNo == 0.25m
                )
            );
    }

    [Theory]
    [InlineData(MessageTypes.BetPlaced)]
    [InlineData("something-invented")]
    public async Task An_unrecognised_type_is_rejected_rather_than_ignored(string type)
    {
        await Assert.ThrowsAsync<JsonException>(() =>
            new TestablePriceUpdateConsumer().Dispatch(
                new MessageEnvelope(Guid.NewGuid(), type, "{}"),
                _services
            )
        );

        _handler.DidNotReceive().ProcessPriceUpdateAsync(Arg.Any<PriceUpdateEvent>());
    }
}
