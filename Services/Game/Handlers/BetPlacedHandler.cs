using Contracts;
using Game.GrpcClients;
using Game.Publishers;

namespace Game.Handlers;

public class BetPlacedHandler
{
    private readonly CatalogGrpcClient _catalog_client;
    private readonly BetApprovedPublisher _publisher;

    public BetPlacedHandler(BetApprovedPublisher publisher, CatalogGrpcClient client)
    {
        _publisher = publisher;
        _catalog_client = client;
    }

    public async Task ProcessBetPlacedAsync(BetPlacedEvent betPlaced)
    {
        // 1. BUSINESS LOGIC
        // example rule
        var eventId = betPlaced.EventId;
        var eventPrice = await _catalog_client.GetEventPriceAsync(eventId);

        // 2. create domain result
        var updateResult = await _catalog_client.UpdateEventPriceAsync(
            eventId,
            null,
            null,
            eventPrice.PotSize + (long)betPlaced.Stake //hack converting from decimal to long, TBD
        );
        if (!updateResult.Success)
            throw new InvalidOperationException(
                $"Catalog rejected price update for event {eventId}"
            );

        var approvedEvent = new BetApprovedEvent
        {
            // for now always approve
            IsApproved = true,
            ApprovedAt = DateTime.UtcNow,
            AccId = betPlaced.OwnerId,
            Stake = betPlaced.Stake
        };

        // 3. publish result

        await _publisher.PublishBetApprovedAsync(approvedEvent);
    }
}