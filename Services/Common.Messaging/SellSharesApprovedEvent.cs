using Common.Enums;

namespace Common.Messaging;

public class SellSharesApprovedEvent
{
    public Guid AccId { get; set; }
    public decimal SellPrice { get; set; }
    public bool IsApproved { get; set; }
    public DateTime ApprovedAt { get; set; }
    public Guid EventId { get; set; }
    public MarketOutcome Outcome { get; set; }
    public decimal ShareAmount { get; set; }
}
