namespace Common.Messaging;

public class PriceUpdateEvent
{
    public Guid EventId { get; init; }
    public decimal PriceYes { get; init; }
    public decimal PriceNo { get; init; }
}
