using Contracts;

namespace Catalog.Common.Entities;

public class Event
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required Guid OwnerId { get; set; }
    public decimal PotSizeYes { get; set; }
    public decimal PotSizeNo { get; set; }
    public MarketOutcome? Outcome { get; set; }
}
