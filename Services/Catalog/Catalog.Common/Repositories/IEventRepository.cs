using Catalog.Common.DTOs;

namespace Catalog.Common.Repositories;

public interface IEventRepository
{
    Task<EventDto> CreateEventAsync(CreateEventDto createEventDto, Guid ownerId);
    Task<EventDto?> GetEventByIdAsync(Guid id);
    Task<List<EventDto>> GetEventsAsync();
    Task<bool> UpdateEventAsync(UpdateEventDto updateEventDto);
}
