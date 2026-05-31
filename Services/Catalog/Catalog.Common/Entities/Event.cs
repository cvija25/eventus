namespace Catalog.Common.Entities;

public class Event
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required int OwnerId { get; set; }
    public required int PriceYes { get; set; }
    public required int PriceNo { get; set; }
    public required int PotSize { get; set; }
}
