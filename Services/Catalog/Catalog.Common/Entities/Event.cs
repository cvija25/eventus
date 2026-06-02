namespace Catalog.Common.Entities;

public class Event
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required Guid OwnerId { get; set; }
    public required int PriceYes { get; set; }
    public required int PriceNo { get; set; }
    public required int PotSize { get; set; }
}
