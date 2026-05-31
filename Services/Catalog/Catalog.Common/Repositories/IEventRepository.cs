using Catalog.Common.DTOs;

namespace Catalog.Common.Repositories;

public interface IEventRepository
{
    Task<EventDTO> CreateEventAsync(CreateEventDTO createEventDto, int ownerId);
    Task<EventDTO?> GetEventByIdAsync(int id);
    Task<List<EventDTO>> GetEventsAsync();
    Task<bool> UpdateEventAsync(UpdateEventDTO updateEventDto);
}
