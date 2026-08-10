namespace Catalog.Common.DTOs;

public record UpdateEventDto(
    Guid Id,
    int? PriceYes = null,
    int? PriceNo = null,
    int? PotSize = null,
    decimal? PotSizeYes = null,
    decimal? PotSizeNo = null,
    string? Title = null
);
