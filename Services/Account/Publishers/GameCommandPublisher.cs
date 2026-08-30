using Common.Messaging;
using Microsoft.Extensions.Options;

namespace Account.Publishers;

public class GameCommandPublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<GameCommandPublisher> logger
) : MessageQueuePublisher(options, logger), IGameCommandPublisher
{
    protected override string QueueName => RabbitMQConstants.GameCommandQueue;

    public async Task PublishBetPlacedAsync(BetPlacedEvent evt)
    {
        await PublishAsync(MessageTypes.BetPlaced, evt);
    }

    public async Task PublishSellSharesAsync(SellSharesEvent evt)
    {
        await PublishAsync(MessageTypes.SellShares, evt);
    }
}
