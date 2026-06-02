namespace Catalog.Common.DTOs;

public record UpdateEventDto(Guid Id, string Title, int PriceYes, int PriceNo, int PotSize);
