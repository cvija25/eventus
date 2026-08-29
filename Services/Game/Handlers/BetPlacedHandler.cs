using System.Globalization;
using Common.Enums;
using Common.Messaging;
using Game.GrpcClients;
using Game.Publishers;

namespace Game.Handlers;

public class BetPlacedHandler
{
    private readonly CatalogGrpcClient _catalog_client;
    private readonly CommandApprovedPublisher _publisher;
    private readonly ILogger<BetPlacedHandler> _logger;

    public BetPlacedHandler(
        CommandApprovedPublisher publisher,
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

        var poolYes = decimal.Parse(market.PoolYes, CultureInfo.InvariantCulture);

        var poolNo = decimal.Parse(market.PoolNo, CultureInfo.InvariantCulture);

        var pot = decimal.Parse(market.Pot, CultureInfo.InvariantCulture);
        var stake = betPlaced.Stake;

        // increase pot
        pot += stake;

        //mint shares
        poolYes += stake;
        poolNo += stake;
        var sharesReceived = 0m;
        if (betPlaced.Outcome == MarketOutcome.Yes)
        {
            var newPoolYes = 1 / poolNo;
            sharesReceived = poolYes - newPoolYes;
            poolYes = newPoolYes;
        }
        else
        {
            var newPoolNo = 1 / poolYes;
            sharesReceived = poolNo - newPoolNo;
            poolNo = newPoolNo;
        }

        var updateResult = await _catalog_client.UpdateEventPriceAsync(
            eventId,
            pot: pot,
            poolYes: poolYes,
            poolNo: poolNo
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
