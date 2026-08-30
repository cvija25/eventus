using System.Text;
using System.Text.Json;
using Common.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Catalog.GRPC.Publishers;

public class PriceUpdatePublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<PriceUpdatePublisher> logger
) : MessageQueuePublisher(options, logger), IPriceUpdatePublisher
{
    protected override string QueueName => RabbitMQConstants.PriceUpdateQueue;

    public async Task PublishPriceUpdateAsync(PriceUpdateEvent evt)
    {
        await PublishAsync(MessageTypes.PriceUpdate, evt);
    }
}
