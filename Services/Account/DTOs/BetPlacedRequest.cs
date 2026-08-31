using Common.Enums;

namespace Account.DTOs;

public class BetPlacedRequest
{
    public decimal Stake { get; set; }
    public Guid EventId { get; set; }
    public MarketOutcome Outcome { get; set; }
    public decimal ExpectedPrice { get; set; }
    public decimal? SlippageDelta { get; set; }
}
