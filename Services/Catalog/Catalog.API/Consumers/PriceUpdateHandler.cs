using Catalog.API.Services;
using Catalog.Common.Repositories;
using Common.Messaging;

namespace Catalog.API.Consumers;

public class PriceUpdateHandler(
    ISseBroadcaster sseBroadcaster,
    IPriceHistoryRepository historyRepository
) : IPriceUpdateHandler
{
    public async void ProcessPriceUpdateAsync(PriceUpdateEvent evt)
    {
        sseBroadcaster.PublishPriceUpdate(evt.EventId, evt.PriceYes, evt.PriceNo);
        await historyRepository.AddPrice(evt.EventId, evt.PriceYes, evt.PriceNo, DateTime.UtcNow);
    }
}
