using Common.Messaging;

namespace Catalog.API.Consumers;

public interface IPriceUpdateHandler
{
    public void ProcessPriceUpdate(PriceUpdateEvent evt);
}
