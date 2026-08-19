using Contracts;

namespace Account.Entities;

public class Transaction
{
    public Guid TransactionId { get; set; }
    public Guid AccountId { get; set; }
    public Guid EventId { get; set; }
    public decimal ShareAmount { get; set; }
    public MarketOutcome Outcome { get; set; }
}
