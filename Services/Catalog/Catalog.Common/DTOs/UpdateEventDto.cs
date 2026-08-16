namespace Catalog.Common.DTOs;

public record UpdateEventDto(
    Guid Id,
    decimal? PotSizeYes = null,
    decimal? PotSizeNo = null,
    string? Title = null
);
