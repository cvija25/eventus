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

    public decimal calculatePayout(decimal reserveYes, decimal reserveNo, decimal sharesAmount)
    {
        var sum = reserveYes + reserveNo + sharesAmount;
        var discriminant = sum * sum - 4 * sharesAmount * reserveNo;
        var x = (sum - (decimal)Math.Sqrt((double)discriminant)) / 2;

        return x;
    }

    public async Task ProcessSellSharesAsync(SellSharesEvent sellShares)
    {
        var eventId = sellShares.EventId;
        var market = await _catalog_client.GetEventPriceAsync(eventId);
        var poolYes = decimal.Parse(market.PoolYes, CultureInfo.InvariantCulture);
        var poolNo = decimal.Parse(market.PoolNo, CultureInfo.InvariantCulture);
        var pot = decimal.Parse(market.Pot, CultureInfo.InvariantCulture);

        var sellPrice = calculatePayout(poolYes, poolNo, sellShares.Shares);
        poolYes += sellShares.Shares - sellPrice;
        poolNo -= sellPrice;
        pot -= sellPrice;

        var updateResult = await _catalog_client.UpdateEventPriceAsync(
            eventId,
            pot: pot,
            poolYes: poolYes,
            poolNo: poolNo
        );

        if (!updateResult.Success)
            throw new InvalidOperationException($"Catalog rejected pot update for event {eventId}");

        var priceChangedEvent = new PriceChangedEvent
        {
            EventId = eventId,
            PriceYes = poolNo / (poolYes + poolNo),
            PriceNo = poolYes / (poolYes + poolNo),
        };
        _publisher.PublishPriceChangedAsync(priceChangedEvent);

        _logger.LogInformation(
            "Shares sold: EventId={EventId}, AccountId={AccountId}, Outcome={Outcome}, Shares={Shares}, SellPrice={SellPrice}",
            sellShares.EventId,
            sellShares.OwnerId,
            sellShares.Outcome,
            sellShares.Shares,
            sellPrice
        );

        var approvedEvent = new SellSharesApprovedEvent
        {
            IsApproved = true,
            ApprovedAt = DateTime.UtcNow,
            AccId = sellShares.OwnerId,
            SellPrice = sellPrice,
            EventId = sellShares.EventId,
            Outcome = sellShares.Outcome,
            ShareAmount = sellShares.Shares,
        };

        await _publisher.PublishSellSharesApprovedAsync(approvedEvent);
    }
}
