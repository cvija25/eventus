using Common.Messaging;
using Contracts.Messaging;
using Microsoft.Extensions.Options;

namespace Catalog.GRPC.Publishers;

public class PriceUpdatePublisher(
    IOptions<KafkaOptions> options,
    ILogger<PriceUpdatePublisher> logger
) : MessageQueueKafkaPublisher(options, logger), IPriceUpdatePublisher
{
    protected override string TopicName => KafkaConstants.PriceUpdateTopic;

    public Task PublishPriceUpdateAsync(PriceUpdateEvent evt) =>
        PublishAsync(MessageTypes.PriceUpdate, evt, evt.EventId.ToString());
}
