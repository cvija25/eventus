using System.Text.Json;
using Common.Messaging;
using Microsoft.Extensions.Options;

namespace Account.Consumers;

public class CommandApprovedConsumer(
    ILogger<CommandApprovedConsumer> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> rabbitOptions
) : MessageQueueConsumer(logger, scopeFactory, rabbitOptions)
{
    protected override string QueueName => RabbitMQConstants.CommandApprovedQueue;

    protected override async Task HandleAsync(
        MessageEnvelope envelope,
        IServiceProvider services,
        CancellationToken ct
    )
    {
        var handler = services.GetRequiredService<ICommandApprovedHandler>();
        switch (envelope.Type)
        {
            case MessageTypes.BetApproved:
                await handler.ProcessBetAsync(envelope.Deserialize<BetApprovedEvent>(), ct);
                break;
            case MessageTypes.SellSharesApproved:
                await handler.ProcessSaleAsync(envelope.Deserialize<SellSharesApprovedEvent>(), ct);
                break;
            default:
                throw new JsonException("Unknown command type.");
        }
    }
}
