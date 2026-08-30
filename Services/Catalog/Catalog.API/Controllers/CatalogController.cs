using Catalog.API.Publishers;
using Catalog.API.Services;
using Catalog.Common.DTOs;
using Catalog.Common.Repositories;
using Common.Enums;
using Common.Messaging;
using Common.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.API.Controllers;

[ApiController]
[Route("/api/v1/catalog/events")]
public class CatalogController : ControllerBase
{
    private readonly IEventRepository _eventRepository;
    private readonly IEventResolvedPublisher _eventResolvedPublisher;
    private readonly ISseBroadcaster _sseBroadcaster;
    private readonly ICurrentUser _currentUser;
    private ILogger<CatalogController> _logger;

    public CatalogController(
        IEventRepository eventRepository,
        IEventResolvedPublisher publisher,
        ISseBroadcaster sseBroadcaster,
        ICurrentUser currentUser,
        ILogger<CatalogController> logger
    )
    {
        _eventRepository = eventRepository;
        _eventResolvedPublisher = publisher;
        _sseBroadcaster = sseBroadcaster;
        _currentUser = currentUser;
        _logger = logger;
    }

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
        if (_currentUser.UserId is not { } ownerId)
            return Unauthorized();

        var ev = await _eventRepository.CreateEventAsync(dto, ownerId);
        return Created($"/api/v1/catalog/events/{ev.Id}", ev);
    }

    [HttpPost("resolve")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<bool>> ResolveEvent([FromBody] ResolveEventDto dto)
    {
        if (dto.Outcome != MarketOutcome.Yes && dto.Outcome != MarketOutcome.No)
            return BadRequest("Invalid outcome. Must be 1 (YES) or 2 (NO).");

        if (_currentUser.UserId is not { } userId)
            return Unauthorized();

        var ev = await _eventRepository.GetEventByIdAsync(dto.Id);

        if (ev is null)
            return NotFound();

        if (ev.OwnerId != userId)
            return Forbid();

        await _eventRepository.ResolveEventAsync(dto);
        var mqEvent = new EventResolvedEvent { EventId = dto.Id, Outcome = dto.Outcome };
        await _eventResolvedPublisher.PublishEventResolvedAsync(mqEvent);

        return Ok();
    }

    [HttpGet("stream")]
    public async Task StreamPrices()
    {
        await _sseBroadcaster.SubscribeAsync(Response, HttpContext.RequestAborted);
    }
}
