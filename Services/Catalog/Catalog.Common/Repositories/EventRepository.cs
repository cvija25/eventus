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

    private static decimal? SafePrice(decimal numerator, decimal denominator)
    {
        if (denominator == 0m)
            return null;

        return numerator / denominator;
    }

    private static EventDto ToEventDto(Event ev)
    {
        var dto = new EventDto
        {
            Id = ev.Id,
            Title = ev.Title,
            OwnerId = ev.OwnerId,
            PotSizeYes = ev.PotSizeYes,
            PotSizeNo = ev.PotSizeNo,
            PotSize = ev.PotSizeYes + ev.PotSizeNo,
            PriceYes = SafePrice(ev.PotSizeYes, ev.PotSizeYes + ev.PotSizeNo),
            PriceNo = SafePrice(ev.PotSizeNo, ev.PotSizeYes + ev.PotSizeNo)
        };

        return dto;
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
        return ToEventDto(newEvent);
    }

    public async Task<EventDto?> GetEventByIdAsync(Guid id)
    {
        var ev = await _context.Events.FindAsync(id);
        if (ev is null)
            return null;

        return ToEventDto(ev);
    }

    public async Task<List<EventDto>> GetEventsAsync()
    {
        var events = await _context.Events.ToListAsync();
        return events.Select(ToEventDto).ToList();
    }

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
