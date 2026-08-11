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
    private const int InitialPotSize = 2;

    public EventRepository(IEventContext context, IMapper mapper)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<EventDto> CreateEventAsync(CreateEventDto createEventDto, Guid ownerId)
    {
        var newEvent = new Event
        {
            Id = Guid.NewGuid(),
            Title = createEventDto.Title,
            OwnerId = ownerId,
            PriceYes = InitialPrice,
            PriceNo = InitialPrice,
            PotSize = InitialPotSize,
            PotSizeYes = 1m,
            PotSizeNo = 1m,
        };
        _context.Events.Add(newEvent);
        await _context.SaveChangesAsync();
        return _mapper.Map<EventDto>(newEvent);
    }

    public async Task<EventDto?> GetEventByIdAsync(Guid id)
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

        if (updateEventDto.PriceYes.HasValue)
            ev.PriceYes = updateEventDto.PriceYes.Value;
        if (updateEventDto.PriceNo.HasValue)
            ev.PriceNo = updateEventDto.PriceNo.Value;
        if (updateEventDto.PotSize.HasValue)
            ev.PotSize = updateEventDto.PotSize.Value;
        if (updateEventDto.PotSizeYes.HasValue) 
            ev.PotSizeYes = updateEventDto.PotSizeYes.Value;
        if (updateEventDto.PotSizeNo.HasValue) 
            ev.PotSizeNo = updateEventDto.PotSizeNo.Value;
        if (updateEventDto.Title != null)
            ev.Title = updateEventDto.Title;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResolveEventAsync(ResolveEventDto resolveEventDto)
    {
        var ev = await _context.Events.FindAsync(resolveEventDto.Id);
        if (ev is null)
            return false;
        ev.Outcome = resolveEventDto.Outcome;
        await _context.SaveChangesAsync();
        return true;
    }
}
