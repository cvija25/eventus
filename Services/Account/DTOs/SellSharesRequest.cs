using Common.Enums;

namespace Account.DTOs;

public class SellSharesRequest
{
    public decimal Shares { get; set; }
    public Guid EventId { get; set; }
    public MarketOutcome Outcome { get; set; }
}
