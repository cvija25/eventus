using Common.Enums;

namespace Common.Messaging;

public class SellSharesEvent
{
    public Guid EventId { get; set; }
    public Guid OwnerId { get; set; }
    public decimal Shares { get; set; }
    public MarketOutcome Outcome { get; set; }
    public decimal ExpectedPrice { get; set; }
    public decimal? SlippageDelta { get; set; }
    public decimal? SpotPriceWindow { get; set; }
}
