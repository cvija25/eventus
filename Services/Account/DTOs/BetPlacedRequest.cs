using Contracts;

namespace Account.DTOs;

public class BetPlacedRequest
{
    public decimal Stake { get; set; }
    public Guid EventId { get; set; }
    public MarketOutcome Outcome { get; set; }
}
