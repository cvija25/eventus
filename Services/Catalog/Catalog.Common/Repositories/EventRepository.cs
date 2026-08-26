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
    private const decimal InitialPot = 1m;
    private const decimal InitialPool = 1m;

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
            Pot = InitialPot,
            PoolYes = InitialPool,
            PoolNo = InitialPool,
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
        if (updateEventDto.Pot != null)
            ev.Pot = updateEventDto.Pot.Value;
        if (updateEventDto.PoolYes.HasValue)
            ev.PoolYes = updateEventDto.PoolYes.Value;
        if (updateEventDto.PoolNo.HasValue)
            ev.PoolNo = updateEventDto.PoolNo.Value;
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
