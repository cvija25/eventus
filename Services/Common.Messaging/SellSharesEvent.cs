using Common.Enums;

namespace Common.Messaging;

public class SellSharesEvent
{
    public Guid EventId { get; set; }
    public Guid OwnerId { get; set; }
    public decimal Shares { get; set; }
    public MarketOutcome Outcome { get; set; }
}
