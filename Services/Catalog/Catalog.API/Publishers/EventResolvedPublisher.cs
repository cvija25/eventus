using System.Text;
using System.Text.Json;
using Common.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Catalog.API.Publishers;

public class EventResolvedPublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<EventResolvedPublisher> logger
) : MessageQueuePublisher(options, logger), IEventResolvedPublisher
{
    protected override string QueueName => RabbitMQConstants.EventResolvedQueue;

    public async Task PublishEventResolvedAsync(EventResolvedEvent evt)
    {
        await PublishAsync(MessageTypes.EventResolved, evt);
    }
}
