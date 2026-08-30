using System.Globalization;
using Catalog.GRPC;
using Common.Messaging;
using Game.GrpcClients;
using Game.Publishers;

namespace Game.Tests;

/// <summary>
/// A stand-in for Catalog that actually holds the market state, so a buy and a later sell see
/// each other's effects. That makes round-trip assertions (buy then sell the shares back)
/// possible, which a call-verifying mock could not express.
/// </summary>
internal sealed class FakeMarket : ICatalogGrpcClient
{
    public decimal Pot { get; set; } = 1m;
    public decimal PoolYes { get; set; } = 1m;
    public decimal PoolNo { get; set; } = 1m;

    public bool AcceptUpdates { get; set; } = true;
    public int UpdateAttempts { get; private set; }

    public Task<GetEventPriceResponse> GetEventPriceAsync(Guid eventId) =>
        Task.FromResult(
            new GetEventPriceResponse
            {
                Pot = Format(Pot),
                PoolYes = Format(PoolYes),
                PoolNo = Format(PoolNo),
            }
        );

    public Task<UpdateEventPriceResponse> UpdateEventPriceAsync(
        Guid eventId,
        decimal? pot = null,
        decimal? poolYes = null,
        decimal? poolNo = null
    )
    {
        UpdateAttempts++;

        if (!AcceptUpdates)
            return Task.FromResult(new UpdateEventPriceResponse { Success = false });

        Pot = pot ?? Pot;
        PoolYes = poolYes ?? PoolYes;
        PoolNo = poolNo ?? PoolNo;
        return Task.FromResult(new UpdateEventPriceResponse { Success = true });
    }

    /// <summary>Mirrors how the real CatalogGrpcClient puts decimals on the wire.</summary>
    private static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}

internal sealed class FakePublisher : ICommandApprovedPublisher
{
    public List<BetApprovedEvent> Bets { get; } = [];
    public List<SellSharesApprovedEvent> Sales { get; } = [];

    public Task PublishBetApprovedAsync(BetApprovedEvent evt)
    {
        Bets.Add(evt);
        return Task.CompletedTask;
    }

    public Task PublishSellSharesApprovedAsync(SellSharesApprovedEvent evt)
    {
        Sales.Add(evt);
        return Task.CompletedTask;
    }
}
