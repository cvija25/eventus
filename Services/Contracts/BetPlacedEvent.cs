namespace Contracts;

public class BetPlacedEvent
{
    public Guid EventId { get; set; }
    public Guid OwnerId { get; set; }
    public decimal Stake { get; set; }
    public MarketOutcome Outcome {get; set; }
}
