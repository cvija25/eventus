using Catalog.Common.DTOs;

namespace Catalog.Common.Repositories;

public interface IEventRepository
{
    Task<EventDto> CreateEventAsync(CreateEventDto createEventDto, int ownerId);
    Task<EventDto?> GetEventByIdAsync(int id);
    Task<List<EventDto>> GetEventsAsync();
    Task<bool> UpdateEventAsync(UpdateEventDto updateEventDto);
}
