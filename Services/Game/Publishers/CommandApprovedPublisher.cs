using System.Text;
using System.Text.Json;
using Common.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Game.Publishers;

public class CommandApprovedPublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<CommandApprovedPublisher> logger
) : MessageQueuePublisher(options, logger), ICommandApprovedPublisher
{
    protected override string QueueName => RabbitMQConstants.CommandApprovedQueue;

    public async Task PublishBetApprovedAsync(BetApprovedEvent evt)
    {
        await PublishAsync(MessageTypes.BetApproved, evt);
    }

    public async Task PublishSellSharesApprovedAsync(SellSharesApprovedEvent evt)
    {
        await PublishAsync(MessageTypes.SellSharesApproved, evt);
    }
}
