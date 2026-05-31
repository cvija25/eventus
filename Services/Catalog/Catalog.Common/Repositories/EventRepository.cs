using AutoMapper;
using AutoMapper.QueryableExtensions;
using Catalog.Common.Data;
using Catalog.Common.DTOs;
using Catalog.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Common.Repositories;

public class EventRepository : IEventRepository
{
    private readonly IEventContext _context;
    private readonly IMapper _mapper;
    private const int InitialPrice = 50;
    private const int InitialPotSize = 0;

    public EventRepository(IEventContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<EventDto> CreateEventAsync(CreateEventDto createEventDto, int ownerId)
    {
        var newEvent = new Event
        {
            Name = createEventDto.Name,
            OwnerId = ownerId,
            PriceYes = InitialPrice,
            PriceNo = InitialPrice,
            PotSize = InitialPotSize,
        };
        _context.Events.Add(newEvent);
        await _context.SaveChangesAsync();
        return _mapper.Map<EventDto>(newEvent);
    }

    public async Task<EventDto?> GetEventByIdAsync(int id)
    {
        var ev = await _context.Events.FindAsync(id);
        return _mapper.Map<EventDto?>(ev);
    }

    public async Task<List<EventDto>> GetEventsAsync() =>
        await _context.Events.ProjectTo<EventDto>(_mapper.ConfigurationProvider).ToListAsync();

    public async Task<bool> UpdateEventAsync(UpdateEventDto updateEventDto)
    {
        var ev = await _context.Events.FindAsync(updateEventDto.Id);
        if (ev is null)
            return false;

        ev.Name = updateEventDto.Name;
        ev.PriceYes = updateEventDto.PriceYes;
        ev.PriceNo = updateEventDto.PriceNo;
        ev.PotSize = updateEventDto.PotSize;

        await _context.SaveChangesAsync();
        return true;
    }
}
