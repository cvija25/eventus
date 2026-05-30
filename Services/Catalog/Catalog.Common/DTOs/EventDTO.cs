namespace Catalog.Common.DTOs;

public class EventDTO
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public required int OwnerId { get; set; }
    public required int PriceYes { get; set; }
    public required int PriceNo { get; set; }
    public required int PotSize { get; set; }
}
