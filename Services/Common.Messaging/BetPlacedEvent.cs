using Common.Enums;

namespace Common.Messaging;

public class BetPlacedEvent
{
    public Guid EventId { get; set; }
    public Guid OwnerId { get; set; }
    public decimal Stake { get; set; }
    public MarketOutcome Outcome { get; set; }
    public decimal ExpectedPrice { get; set; }
    public decimal? SlippageDelta { get; set; }
    public decimal? SpotPriceWindow { get; set; }
}
