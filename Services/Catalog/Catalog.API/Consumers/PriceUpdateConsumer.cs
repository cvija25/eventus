using System.Text.Json;
using Common.Messaging;
using Microsoft.Extensions.Options;

namespace Catalog.API.Consumers;

public class PriceUpdateConsumer(
    ILogger<PriceUpdateConsumer> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> rabbitOptions
) : MessageQueueConsumer(logger, scopeFactory, rabbitOptions)
{
    protected override string QueueName => RabbitMQConstants.PriceUpdateQueue;

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
                handler.ProcessPriceUpdate(envelope.Deserialize<PriceUpdateEvent>());
                return Task.CompletedTask; //hack
                break;
            default:
                throw new JsonException($"Unknown command result type '{envelope.Type}'.");
        }
    }
}
