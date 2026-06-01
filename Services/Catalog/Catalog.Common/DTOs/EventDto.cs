namespace Catalog.Common.DTOs;

public record EventDto(Guid Id, string Name, Guid OwnerId, int PriceYes, int PriceNo, int PotSize);
