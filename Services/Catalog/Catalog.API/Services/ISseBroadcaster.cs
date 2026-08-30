namespace Catalog.API.Services;

public interface ISseBroadcaster
{
    Task SubscribeAsync(HttpResponse response, CancellationToken cancellationToken);
    void PublishPriceUpdate(Guid eventId, decimal priceYes, decimal priceNo);
}
