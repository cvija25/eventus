using Catalog.API.Services;
using Common.Messaging;

namespace Catalog.API.Consumers;

public class PriceUpdateHandler(ISseBroadcaster sseBroadcaster) : IPriceUpdateHandler
{
    public void ProcessPriceUpdate(PriceUpdateEvent evt)
    {
        sseBroadcaster.PublishPriceUpdate(evt.EventId, evt.PriceYes, evt.PriceNo);
    }
}
