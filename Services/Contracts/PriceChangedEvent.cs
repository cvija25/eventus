namespace Contracts;

public class PriceChangedEvent
{
    public Guid EventId { get; init; }
    public decimal PriceYes { get; init; }
    public decimal PriceNo { get; init; }
}
