namespace Catalog.Common.DTOs;

public record EventDto(Guid Id, string Title, Guid OwnerId, int PriceYes, int PriceNo, int PotSize);
