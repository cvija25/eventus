using Contracts;
using Game.GrpcClients;
using Game.Publishers;

namespace Game.Handlers;

public class BetPlacedHandler
{
    private readonly BetApprovedPublisher _publisher;
    private readonly CatalogGrpcClient _catalog_client;

    public BetPlacedHandler(BetApprovedPublisher publisher, CatalogGrpcClient client)
    {
        _publisher = publisher;
        _catalog_client = client;
    }

    public async Task ProcessBetPlacedAsync(BetPlacedEvent betPlaced)
    {
        // 1. BUSINESS LOGIC
        // example rule
        var eventId = Guid.Parse("bc84e4b5-dab3-4cef-9f70-4ec5f6fad254");
        var eventPrice = await _catalog_client.GetEventPriceAsync(eventId);

        // 2. create domain result
        await _catalog_client.UpdateEventPriceAsync(
            eventId,
            null,
            null,
            eventPrice.PotSize + (long)betPlaced.Stake
        );
        var approvedEvent = new BetApprovedEvent
        {
            // for now always approve
            IsApproved = true,

            ApprovedAt = DateTime.UtcNow,
        };

        // 3. publish result

        await _publisher.PublishBetApprovedAsync(approvedEvent);
    }
}
