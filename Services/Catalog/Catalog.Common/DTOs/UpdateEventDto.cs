namespace Catalog.Common.DTOs;

public record UpdateEventDto(Guid Id, string Name, int PriceYes, int PriceNo, int PotSize);
