namespace Catalog.Common.DTOs;

public record EventDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid OwnerId { get; init; }
    public decimal? PriceYes { get; init; }
    public decimal? PriceNo { get; init; }
    public decimal? PotSize { get; init; }
    public decimal PotSizeYes { get; init; }
    public decimal PotSizeNo { get; init; }
}
