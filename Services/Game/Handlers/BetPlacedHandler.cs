using System.Globalization;
using Contracts;
using Game.GrpcClients;
using Game.Publishers;

namespace Game.Handlers;

public class BetPlacedHandler
{
    private readonly CatalogGrpcClient _catalog_client;
    private readonly BetApprovedPublisher _publisher;
    private readonly ILogger<BetPlacedHandler> _logger;

    public BetPlacedHandler(
        BetApprovedPublisher publisher,
        CatalogGrpcClient client,
        ILogger<BetPlacedHandler> logger
    )
    {
        _publisher = publisher;
        _catalog_client = client;
        _logger = logger;
    }

    public async Task ProcessBetPlacedAsync(BetPlacedEvent betPlaced)
    {
        // 1. BUSINESS LOGIC
        // example rule
        var eventId = betPlaced.EventId;
        var market = await _catalog_client.GetEventPriceAsync(eventId);

        var yesPot = decimal.Parse(market.PotSizeYes, CultureInfo.InvariantCulture);

        var noPot = decimal.Parse(market.PotSizeNo, CultureInfo.InvariantCulture);

        var totalPot = yesPot + noPot;

        // 2. Calculate price BEFORE adding the current stake
        var selectedPrice = betPlaced.Outcome switch
        {
            MarketOutcome.Yes => yesPot / totalPot,
            MarketOutcome.No => noPot / totalPot,
            _ => throw new InvalidOperationException(
                $"Invalid market outcome: {betPlaced.Outcome}"
            ),
        };
        // TODO: user must by a whole number of shares(?)
        var sharesReceived = betPlaced.Stake / selectedPrice;

        // 3. Add stake to the selected outcome pot
        if (betPlaced.Outcome == MarketOutcome.Yes)
            yesPot += betPlaced.Stake;
        else
            noPot += betPlaced.Stake;

        var updateResult = await _catalog_client.UpdateEventPriceAsync(
            eventId,
            potSizeYes: yesPot,
            potSizeNo: noPot
        );

        if (!updateResult.Success)
            throw new InvalidOperationException($"Catalog rejected pot update for event {eventId}");

        // 5. Log the calculated shares
        _logger.LogInformation(
            "Bet executed: EventId={EventId}, AccountId={AccountId}, Outcome={Outcome}, Stake={Stake}, SharesReceived={SharesReceived}",
            betPlaced.EventId,
            betPlaced.OwnerId,
            betPlaced.Outcome,
            betPlaced.Stake,
            sharesReceived
        );

        // 6. Publish approval
        var approvedEvent = new BetApprovedEvent
        {
            IsApproved = true,
            ApprovedAt = DateTime.UtcNow,
            AccId = betPlaced.OwnerId,
            Stake = betPlaced.Stake,
            EventId = betPlaced.EventId,
            Outcome = betPlaced.Outcome,
            ShareAmount = sharesReceived,
        };

        await _publisher.PublishBetApprovedAsync(approvedEvent);
    }
}
