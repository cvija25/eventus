namespace Catalog.Common.DTOs;

public record EventDto(int Id, string Name, int OwnerId, int PriceYes, int PriceNo, int PotSize);
