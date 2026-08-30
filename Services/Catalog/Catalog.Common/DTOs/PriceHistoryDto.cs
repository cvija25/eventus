namespace Catalog.Common.DTOs;

public record PriceHistoryDto(Guid EventId, decimal PriceYes, decimal PriceNo, DateTime Timestamp);
