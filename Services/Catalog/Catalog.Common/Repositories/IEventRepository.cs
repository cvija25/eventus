using Catalog.Common.DTOs;

namespace Catalog.Common.Repositories;

public interface IEventRepository
{
    Task<EventDto> CreateEventAsync(CreateEventDto createEventDto, Guid ownerId);
    Task<EventDto?> GetEventByIdAsync(Guid id);
    Task<List<EventDto>> GetEventsAsync();
    Task<EventDto?> UpdateEventAsync(UpdateEventDto updateEventDto);
    Task<bool> ResolveEventAsync(ResolveEventDto resolveEventDto);
}
