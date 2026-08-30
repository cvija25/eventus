using System.Text;
using System.Text.Json;
using Account.Data;
using Account.Repositories;
using Common.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Account.Consumers;

public class EventResolvedEventConsumer(
    ILogger<EventResolvedEventConsumer> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> rabbitOptions
) : MessageQueueConsumer(logger, scopeFactory, rabbitOptions)
{
    protected override string QueueName => RabbitMQConstants.EventResolvedQueue;

    protected override async Task HandleAsync(
        MessageEnvelope envelope,
        IServiceProvider services,
        CancellationToken ct
    )
    {
        var handler = services.GetRequiredService<IEventResolvedHandler>();
        if (envelope.Type != MessageTypes.EventResolved)
            throw new JsonException("Unexpected event type");
        await handler.ProcessEventResolvedAsync(envelope.Deserialize<EventResolvedEvent>(), ct);
    }
}
