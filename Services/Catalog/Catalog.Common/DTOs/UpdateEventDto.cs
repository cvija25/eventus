namespace Catalog.Common.DTOs;

public record UpdateEventDto(Guid Id, int PriceYes, int PriceNo, int PotSize, string Title = "");
