using Common.Messaging;

namespace Catalog.GRPC.Publishers;

public interface IPriceUpdatePublisher
{
    public Task PublishPriceUpdateAsync(PriceUpdateEvent evt);
}
