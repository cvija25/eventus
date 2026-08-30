using System.Globalization;
using Common.Enums;
using Common.Messaging;
using Game.GrpcClients;
using Game.Publishers;

namespace Game.Consumers;

public class GameCommandHandler(
    CatalogGrpcClient catalogGrpcClient,
    ICommandApprovedPublisher commandApprovedPublisher,
    ILogger<GameCommandHandler> logger
) : IGameCommandHandler
{
    public async Task ProcessBetPlacedAsync(BetPlacedEvent betPlaced)
    {
        var eventId = betPlaced.EventId;
        var market = await catalogGrpcClient.GetEventPriceAsync(eventId);

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

        var updateResult = await catalogGrpcClient.UpdateEventPriceAsync(
            eventId,
            pot: pot,
            poolYes: poolYes,
            poolNo: poolNo
        );

        if (!updateResult.Success)
            throw new InvalidOperationException($"Catalog rejected pot update for event {eventId}");

        // 5. Log the calculated shares
        logger.LogInformation(
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

        await commandApprovedPublisher.PublishBetApprovedAsync(approvedEvent);
    }

    public async Task ProcessSellSharesAsync(SellSharesEvent sellShares)
    {
        var eventId = sellShares.EventId;
        var market = await catalogGrpcClient.GetEventPriceAsync(eventId);
        var poolYes = decimal.Parse(market.PoolYes, CultureInfo.InvariantCulture);
        var poolNo = decimal.Parse(market.PoolNo, CultureInfo.InvariantCulture);
        var pot = decimal.Parse(market.Pot, CultureInfo.InvariantCulture);

        var sellPrice = CalculatePayout(poolYes, poolNo, sellShares.Shares);
        poolYes += sellShares.Shares - sellPrice;
        poolNo -= sellPrice;
        pot -= sellPrice;

        var updateResult = await catalogGrpcClient.UpdateEventPriceAsync(
            eventId,
            pot: pot,
            poolYes: poolYes,
            poolNo: poolNo
        );

        if (!updateResult.Success)
            throw new InvalidOperationException($"Catalog rejected pot update for event {eventId}");

        logger.LogInformation(
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

        await commandApprovedPublisher.PublishSellSharesApprovedAsync(approvedEvent);
    }

    private decimal CalculatePayout(decimal reserveYes, decimal reserveNo, decimal sharesAmount)
    {
        var sum = reserveYes + reserveNo + sharesAmount;
        var discriminant = sum * sum - 4 * sharesAmount * reserveNo;
        var x = (sum - (decimal)Math.Sqrt((double)discriminant)) / 2;

        return x;
    }
}
