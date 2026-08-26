namespace Catalog.Common.DTOs;

public record UpdateEventDto(
    Guid Id,
    decimal? Pot = null,
    decimal? PoolYes = null,
    decimal? PoolNo = null,
    string? Title = null
);
