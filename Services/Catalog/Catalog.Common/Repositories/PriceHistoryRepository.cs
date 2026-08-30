using AutoMapper;
using Catalog.Common.Data;
using Catalog.Common.DTOs;
using Catalog.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Common.Repositories;

public class PriceHistoryRepository : IPriceHistoryRepository
{
    private readonly IHistoryContext _context;
    private readonly IMapper _mapper;

    public PriceHistoryRepository(IHistoryContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<List<PriceHistoryDto>> GetHistory(Guid eventId)
    {
        var history = await _context
            .Histories.Where(h => h.EventId == eventId)
            .OrderBy(h => h.Timestamp)
            .ToListAsync();

        return _mapper.Map<List<PriceHistoryDto>>(history);
    }

    public async Task<PriceHistoryDto> AddPrice(
        Guid eventId,
        decimal priceYes,
        decimal priceNo,
        DateTime timestamp
    )
    {
        var newEvent = new PriceHistory
        {
            EventId = eventId,
            PriceYes = priceYes,
            PriceNo = priceNo,
            Timestamp = timestamp,
        };
        _context.Histories.Add(newEvent);
        await _context.SaveChangesAsync();
        return _mapper.Map<PriceHistoryDto>(newEvent);
    }
}
