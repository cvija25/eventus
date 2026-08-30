using System.Text.Json;
using Common.Messaging;
using Microsoft.Extensions.Options;

namespace Game.Consumers;

public class GameCommandConsumer(
    ILogger<GameCommandConsumer> logger,
    IOptions<RabbitMqOptions> rabbitOptions,
    IServiceScopeFactory scopeFactory
) : MessageQueueConsumer(logger, scopeFactory, rabbitOptions)
{
    protected override string QueueName => RabbitMQConstants.GameCommandQueue;

    protected override async Task HandleAsync(
        MessageEnvelope envelope,
        IServiceProvider services,
        CancellationToken ct
    )
    {
        var handler = services.GetRequiredService<IGameCommandHandler>();
        switch (envelope.Type)
        {
            case MessageTypes.BetPlaced:
                await handler.ProcessBetPlacedAsync(envelope.Deserialize<BetPlacedEvent>());
                break;
            case MessageTypes.SellShares:
                await handler.ProcessSellSharesAsync(envelope.Deserialize<SellSharesEvent>());
                break;
            default:
                throw new JsonException($"Unknown game command type '{envelope.Type}'.");
        }
    }
}
