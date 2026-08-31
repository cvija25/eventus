using System.Globalization;
using Common.Enums;
using Common.Messaging;
using Game.GrpcClients;
using Game.Publishers;

namespace Game.Consumers;

public class GameCommandHandler(
    ICatalogGrpcClient catalogGrpcClient,
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

        // Simulating changes - calculating how many shares user will recieve
        var simulatedPoolYes = poolYes + stake;
        var simulatedPoolNo = poolNo + stake;
        var sharesReceived = 0m;

        if (betPlaced.Outcome == MarketOutcome.Yes)
        {
            var newPoolYes = 1 / simulatedPoolNo;
            sharesReceived = simulatedPoolYes - newPoolYes;
            simulatedPoolYes = newPoolYes;
        }
        else
        {
            var newPoolNo = 1 / simulatedPoolYes;
            sharesReceived = simulatedPoolNo - newPoolNo;
            simulatedPoolNo = newPoolNo;
        }

        // Optional slippage check
        if (betPlaced.SlippageDelta.HasValue)
        {
            var effectivePrice = sharesReceived > 0 ? stake / sharesReceived : 0;
            var expectedPrice = betPlaced.ExpectedPrice;
            var slippageDelta = betPlaced.SlippageDelta;

            if (Math.Abs(effectivePrice - expectedPrice) > slippageDelta)
            {
                logger.LogWarning(
                    "Bet rejected due to slippage: EventId={EventId}, ExpectedPrice={ExpectedPrice}, ActualPrice={ActualPrice}",
                    eventId,
                    expectedPrice,
                    effectivePrice
                );

                // Transaction rejected
                var rejectedEvent = new BetApprovedEvent
                {
                    IsApproved = false,
                    ApprovedAt = DateTime.UtcNow,
                    AccId = betPlaced.OwnerId,
                    Stake = betPlaced.Stake,
                    EventId = betPlaced.EventId,
                    Outcome = betPlaced.Outcome,
                    ShareAmount = 0,
                };
                await commandApprovedPublisher.PublishBetApprovedAsync(rejectedEvent);
                return;
            }
        }

        // Applying simulated state
        pot += stake;
        poolYes = simulatedPoolYes;
        poolNo = simulatedPoolNo;

        var updateResult = await catalogGrpcClient.UpdateEventPriceAsync(
            eventId,
            pot: pot,
            poolYes: poolYes,
            poolNo: poolNo
        );

        if (!updateResult.Success)
            throw new InvalidOperationException($"Catalog rejected pot update for event {eventId}");

        // Log the calculated shares
        logger.LogInformation(
            "Bet executed: EventId={EventId}, AccountId={AccountId}, Outcome={Outcome}, Stake={Stake}, SharesReceived={SharesReceived}",
            betPlaced.EventId,
            betPlaced.OwnerId,
            betPlaced.Outcome,
            betPlaced.Stake,
            sharesReceived
        );

        // Publish approval
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

        var sellPrice = CalculatePayout(poolYes, poolNo, sellShares.Shares, sellShares.Outcome);

        // Optional slippage check
        if (sellShares.SlippageDelta.HasValue)
        {
            var effectivePrice = sellShares.Shares > 0 ? sellPrice / sellShares.Shares : 0;
            var expectedPrice = sellShares.ExpectedPrice;
            var slippageDelta = sellShares.SlippageDelta;

            if (Math.Abs(effectivePrice - expectedPrice) > slippageDelta)
            {
                logger.LogWarning(
                    "Sell rejected due to slippage: EventId={EventId}, ExpectedPrice={ExpectedPrice}, ActualPrice={ActualPrice}",
                    eventId,
                    expectedPrice,
                    effectivePrice
                );

                var rejectedEvent = new SellSharesApprovedEvent
                {
                    IsApproved = false,
                    ApprovedAt = DateTime.UtcNow,
                    AccId = sellShares.OwnerId,
                    SellPrice = 0,
                    EventId = sellShares.EventId,
                    Outcome = sellShares.Outcome,
                    ShareAmount = sellShares.Shares,
                };
                await commandApprovedPublisher.PublishSellSharesApprovedAsync(rejectedEvent);
                return;
            }
        }

        if (sellShares.Outcome == MarketOutcome.Yes)
        {
            poolYes += sellShares.Shares - sellPrice;
            poolNo -= sellPrice;
        }
        else
        {
            poolNo += sellShares.Shares - sellPrice;
            poolYes -= sellPrice;
        }

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

    private decimal CalculatePayout(
        decimal poolYes,
        decimal poolNo,
        decimal sharesAmount,
        MarketOutcome outcome
    )
    {
        // reserveIn = pool the sold shares are returned to
        // reserveOut = pool the payout is drawn from
        var (reserveIn, reserveOut) =
            outcome == MarketOutcome.Yes ? (poolYes, poolNo) : (poolNo, poolYes);

        var sum = reserveIn + reserveOut + sharesAmount;
        var discriminant = sum * sum - 4 * sharesAmount * reserveOut;

        if (discriminant < 0)
            throw new InvalidOperationException(
                $"CPMM sell payout discriminant negative (sum={sum}, shares={sharesAmount}, reserveOut={reserveOut}); pools may be too small for this sell size."
            );

        var x = (sum - (decimal)Math.Sqrt((double)discriminant)) / 2;

        return x;
    }
}
