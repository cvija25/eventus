using System.IdentityModel.Tokens.Jwt;
using Catalog.Common.DTOs;
using Catalog.Common.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.API.Controllers;

[ApiController]
[Route("/api/v1/catalog/events")]
public class CatalogController : ControllerBase
{
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EventDto>> GetEvent(Guid id)
    {
        var ev = await _eventRepository.GetEventByIdAsync(id);
        return ev is null ? NotFound() : Ok(ev);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EventDto>>> GetAllEvents()
    {
        var events = await _eventRepository.GetEventsAsync();
        return Ok(events);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<EventDto>> CreateEvent([FromBody] CreateEventDto dto)
    {
        var ownerId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var ev = await _eventRepository.CreateEventAsync(dto, ownerId);
        return Created($"/api/v1/catalog/events/{ev.Id}", ev);
    }

    private readonly IEventRepository _eventRepository;

    public CatalogController(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }
}
