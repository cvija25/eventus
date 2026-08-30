using Catalog.Common.DTOs;

namespace Catalog.Common.Repositories;

public interface IPriceHistoryRepository
{
    Task<List<PriceHistoryDto>> GetHistory(Guid eventId);
    Task<PriceHistoryDto> AddPrice(
        Guid eventId,
        decimal priceYes,
        decimal priceNo,
        DateTime timestamp
    );
}
