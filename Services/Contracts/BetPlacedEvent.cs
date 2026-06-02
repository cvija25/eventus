namespace Contracts;

public class BetPlacedEvent
{
    public Guid BetId { get; set; }
    public Guid UserId { get; set; }
    public decimal Stake { get; set; }
}
