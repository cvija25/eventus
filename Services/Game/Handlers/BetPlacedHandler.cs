using Contracts;
using Game.Publishers;

namespace Game.Handlers;

public class BetPlacedHandler
{
    private readonly BetApprovedPublisher _publisher;

    public BetPlacedHandler(BetApprovedPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task ProcessBetPlacedAsync(BetPlacedEvent betPlaced)
    {
        // 1. BUSINESS LOGIC
        // example rule

        // 2. create domain result

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
