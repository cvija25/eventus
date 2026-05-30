namespace Catalog.Common.DTOs;

public record UpdateEventDTO
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public required int PriceYes { get; set; }
    public required int PriceNo { get; set; }
    public required int PotSize { get; set; }
}