using System.Globalization;
using Contracts;
using Game.GrpcClients;
using Game.Publishers;

namespace Game.Handlers;

public class SellSharesHandler
{
    private readonly CatalogGrpcClient _catalog_client;
    private readonly CommandApprovedPublisher _publisher;
    private readonly ILogger<SellSharesHandler> _logger;

    public SellSharesHandler(
        CommandApprovedPublisher publisher,
        CatalogGrpcClient client,
        ILogger<SellSharesHandler> logger
    )
    {
        _publisher = publisher;
        _catalog_client = client;
        _logger = logger;
    }

    public async Task ProcessSellSharesAsync(SellSharesEvent sellShares)
    {
        var eventId = sellShares.EventId;
        var market = await _catalog_client.GetEventPriceAsync(eventId);
        var yesPot = decimal.Parse(market.PotSizeYes, CultureInfo.InvariantCulture);
        var noPot = decimal.Parse(market.PotSizeNo, CultureInfo.InvariantCulture);
        var totalPot = yesPot + noPot;

        var selectedPrice = sellShares.Outcome switch
        {
            MarketOutcome.Yes => yesPot / totalPot,
            MarketOutcome.No => noPot / totalPot,
            _ => throw new InvalidOperationException(
                $"Invalid market outcome: {sellShares.Outcome}"
            ),
        };
        var grossProceeds = sellShares.Shares * selectedPrice;
        var selectedPot = sellShares.Outcome == MarketOutcome.Yes ? yesPot : noPot;

        if (grossProceeds >= selectedPot)
        {
            await _publisher.PublishSellSharesApprovedAsync(
                new SellSharesApprovedEvent
                {
                    IsApproved = false,
                    ApprovedAt = DateTime.UtcNow,
                    AccId = sellShares.OwnerId,
                    EventId = sellShares.EventId,
                    Outcome = sellShares.Outcome,
                    ShareAmount = sellShares.Shares,
                    SellPrice = selectedPrice,
                }
            );
            return;
        }

        if (sellShares.Outcome == MarketOutcome.Yes)
            yesPot -= grossProceeds;
        else
            noPot -= grossProceeds;

        var updateResult = await _catalog_client.UpdateEventPriceAsync(
            eventId,
            potSizeYes: yesPot,
            potSizeNo: noPot
        );

        if (!updateResult.Success)
            throw new InvalidOperationException($"Catalog rejected pot update for event {eventId}");

        _logger.LogInformation(
            "Shares sold: EventId={EventId}, AccountId={AccountId}, Outcome={Outcome}, Shares={Shares}, SellPrice={SellPrice}",
            sellShares.EventId,
            sellShares.OwnerId,
            sellShares.Outcome,
            sellShares.Shares,
            selectedPrice
        );

        var approvedEvent = new SellSharesApprovedEvent
        {
            IsApproved = true,
            ApprovedAt = DateTime.UtcNow,
            AccId = sellShares.OwnerId,
            SellPrice = selectedPrice,
            EventId = sellShares.EventId,
            Outcome = sellShares.Outcome,
            ShareAmount = sellShares.Shares,
        };

        await _publisher.PublishSellSharesApprovedAsync(approvedEvent);
    }
}
