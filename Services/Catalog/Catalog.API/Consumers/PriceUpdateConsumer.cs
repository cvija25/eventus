using System.Text.Json;
using Common.Messaging;
using Contracts.Messaging;
using Microsoft.Extensions.Options;

namespace Catalog.API.Consumers;

public class PriceUpdateConsumer(
    ILogger<PriceUpdateConsumer> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions
) : MessageQueueKafkaConsumer(logger, scopeFactory, kafkaOptions)
{
    protected override string TopicName => KafkaConstants.PriceUpdateTopic;
    protected override string GroupId => "catalog-price-update";

    protected override Task HandleAsync(
        MessageEnvelope envelope,
        IServiceProvider services,
        CancellationToken ct
    )
    {
        var handler = services.GetRequiredService<IPriceUpdateHandler>();
        switch (envelope.Type)
        {
            case MessageTypes.PriceUpdate:
                handler.ProcessPriceUpdateAsync(envelope.Deserialize<PriceUpdateEvent>());
                return Task.CompletedTask;
            default:
                throw new JsonException($"Unknown command result type '{envelope.Type}'.");
        }
    }
}
