using Contracts;

namespace Catalog.Common.Entities;

public class Event
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required Guid OwnerId { get; set; }
    public required decimal PoolYes { get; set; }
    public required decimal PoolNo { get; set; }
    public required decimal Pot { get; set; }
    public MarketOutcome? Outcome { get; set; }
}
