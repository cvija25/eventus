using Catalog.API.Controllers;
using Catalog.API.Publishers;
using Catalog.API.Services;
using Catalog.Common.DTOs;
using Catalog.Common.Repositories;
using Common.Enums;
using Common.Messaging;
using Common.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Catalog.Tests.Unit;

public class CatalogControllerTests
{
    private readonly IEventRepository _repository = Substitute.For<IEventRepository>();
    private readonly IPriceHistoryRepository _history = Substitute.For<IPriceHistoryRepository>();
    private readonly IEventResolvedPublisher _publisher = Substitute.For<IEventResolvedPublisher>();
    private readonly ISseBroadcaster _broadcaster = Substitute.For<ISseBroadcaster>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly CatalogController _controller;

    private readonly Guid _userId = Guid.NewGuid();

    public CatalogControllerTests()
    {
        _currentUser.UserId.Returns(_userId);
        _controller = new CatalogController(
            _repository,
            _history,
            _publisher,
            _broadcaster,
            _currentUser,
            NullLogger<CatalogController>.Instance
        );
    }

    private static EventDto EventOwnedBy(Guid ownerId, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Title = "Will it rain?",
            OwnerId = ownerId,
            PoolYes = 1m,
            PoolNo = 1m,
            Pot = 1m,
        };

    private Task<ActionResult<bool>> Resolve(Guid id, MarketOutcome outcome) =>
        _controller.ResolveEvent(new ResolveEventDto(id, outcome));

    [Fact]
    public async Task GetEvent_returns_the_event()
    {
        var ev = EventOwnedBy(_userId);
        _repository.GetEventByIdAsync(ev.Id).Returns(ev);

        var result = await _controller.GetEvent(ev.Id);

        Assert.Same(ev, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task GetEvent_is_404_when_it_does_not_exist()
    {
        _repository.GetEventByIdAsync(Arg.Any<Guid>()).Returns((EventDto?)null);

        var result = await _controller.GetEvent(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetAllEvents_returns_the_listing()
    {
        var events = new List<EventDto> { EventOwnedBy(_userId), EventOwnedBy(_userId) };
        _repository.GetEventsAsync().Returns(events);

        var result = await _controller.GetAllEvents();

        Assert.Same(events, Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task CreateEvent_stamps_the_caller_as_owner_and_returns_its_location()
    {
        var created = EventOwnedBy(_userId);
        _repository.CreateEventAsync(Arg.Any<CreateEventDto>(), _userId).Returns(created);

        var result = await _controller.CreateEvent(new CreateEventDto("Will it rain?"));

        var response = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal($"/api/v1/events/{created.Id}", response.Location);
        Assert.Same(created, response.Value);
    }

    [Fact]
    public async Task CreateEvent_is_401_without_a_caller()
    {
        _currentUser.UserId.Returns((Guid?)null);

        var result = await _controller.CreateEvent(new CreateEventDto("Will it rain?"));

        Assert.IsType<UnauthorizedResult>(result.Result);
        await _repository
            .DidNotReceive()
            .CreateEventAsync(Arg.Any<CreateEventDto>(), Arg.Any<Guid>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public async Task ResolveEvent_rejects_an_outcome_outside_the_enum(int outcome)
    {
        var result = await Resolve(Guid.NewGuid(), (MarketOutcome)outcome);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        await _publisher.DidNotReceive().PublishEventResolvedAsync(Arg.Any<EventResolvedEvent>());
    }

    [Fact]
    public async Task ResolveEvent_is_401_without_a_caller()
    {
        _currentUser.UserId.Returns((Guid?)null);

        var result = await Resolve(Guid.NewGuid(), MarketOutcome.Yes);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task ResolveEvent_is_404_when_the_event_is_unknown()
    {
        _repository.GetEventByIdAsync(Arg.Any<Guid>()).Returns((EventDto?)null);

        var result = await Resolve(Guid.NewGuid(), MarketOutcome.Yes);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ResolveEvent_is_forbidden_for_someone_elses_event()
    {
        var ev = EventOwnedBy(Guid.NewGuid());
        _repository.GetEventByIdAsync(ev.Id).Returns(ev);

        var result = await Resolve(ev.Id, MarketOutcome.Yes);

        Assert.IsType<ForbidResult>(result.Result);
        await _repository.DidNotReceive().ResolveEventAsync(Arg.Any<ResolveEventDto>());
        await _publisher.DidNotReceive().PublishEventResolvedAsync(Arg.Any<EventResolvedEvent>());
    }

    [Fact]
    public async Task ResolveEvent_settles_the_market_and_announces_the_outcome()
    {
        var ev = EventOwnedBy(_userId);
        _repository.GetEventByIdAsync(ev.Id).Returns(ev);

        var result = await Resolve(ev.Id, MarketOutcome.No);

        Assert.IsType<OkResult>(result.Result);
        await _repository
            .Received(1)
            .ResolveEventAsync(
                Arg.Is<ResolveEventDto>(d => d.Id == ev.Id && d.Outcome == MarketOutcome.No)
            );
        await _publisher
            .Received(1)
            .PublishEventResolvedAsync(
                Arg.Is<EventResolvedEvent>(e => e.EventId == ev.Id && e.Outcome == MarketOutcome.No)
            );
    }

    [Fact]
    public async Task GetHistory_returns_the_recorded_prices_for_the_event()
    {
        var eventId = Guid.NewGuid();
        List<PriceHistoryDto> recorded =
        [
            new(eventId, 0.5m, 0.5m, DateTime.UtcNow.AddMinutes(-1)),
            new(eventId, 0.75m, 0.25m, DateTime.UtcNow),
        ];
        _history.GetHistory(eventId).Returns(recorded);

        var result = await _controller.GetHistory(eventId);

        Assert.Same(recorded, Assert.IsType<OkObjectResult>(result.Result).Value);
        await _history.Received(1).GetHistory(eventId);
    }

    [Fact]
    public async Task GetHistory_for_an_event_with_no_prices_is_an_empty_list_not_a_404()
    {
        // The frontend charts this directly; a 404 for a market that simply has not traded yet
        // would be an error case for an ordinary, expected state.
        _history.GetHistory(Arg.Any<Guid>()).Returns([]);

        var result = await _controller.GetHistory(Guid.NewGuid());

        Assert.Empty(
            Assert.IsType<List<PriceHistoryDto>>(Assert.IsType<OkObjectResult>(result.Result).Value)
        );
    }

    [Fact]
    public async Task StreamPrices_hands_the_response_to_the_broadcaster()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
        };

        await _controller.StreamPrices();

        await _broadcaster
            .Received(1)
            .SubscribeAsync(_controller.Response, Arg.Any<CancellationToken>());
    }
}
