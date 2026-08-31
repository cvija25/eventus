using Catalog.Common.DTOs;
using Catalog.GRPC;
using Catalog.GRPC.Publishers;
using Catalog.GRPC.Services;
using Common.Enums;
using Common.Messaging;
using Eventus.Testing;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Catalog.Tests.Integration;

/// <summary>
/// The gRPC surface Game drives. Exercised over a real repository and database, with only the
/// message broker faked, because the interesting behaviour is the interplay between what gets
/// persisted and what gets broadcast.
/// </summary>
public class CatalogServiceTests(PostgresFixture postgres) : CatalogDatabaseTest(postgres)
{
    private sealed class RecordingPublisher : IPriceUpdatePublisher
    {
        public List<PriceUpdateEvent> Published { get; } = [];

        public Task PublishPriceUpdateAsync(PriceUpdateEvent evt)
        {
            Published.Add(evt);
            return Task.CompletedTask;
        }
    }

    private readonly RecordingPublisher _publisher = new();
    private readonly TestServerCallContext _callContext = new();

    private CatalogService Service =>
        new(_publisher, NullLogger<CatalogService>.Instance, Repository, Mapper);

    private Task<EventDto> CreateAsync() =>
        Repository.CreateEventAsync(new CreateEventDto("Will it rain?"), Guid.NewGuid());

    private static UpdateEventPriceRequest UpdateRequest(
        Guid id,
        decimal pot,
        decimal poolYes,
        decimal poolNo
    ) =>
        new()
        {
            EventId = id.ToString(),
            Pot = pot.ToString(System.Globalization.CultureInfo.InvariantCulture),
            PoolYes = poolYes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            PoolNo = poolNo.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

    [Fact]
    public async Task GetEventPrice_reports_the_current_market()
    {
        var created = await CreateAsync();

        var response = await Service.GetEventPrice(
            new GetEventPriceRequest { EventId = created.Id.ToString() },
            _callContext
        );

        Assert.Equal("1", response.Pot);
        Assert.Equal("1", response.PoolYes);
        Assert.Equal("1", response.PoolNo);
    }

    [Fact]
    public async Task GetEventPrice_is_a_NotFound_rpc_fault_for_an_unknown_event()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            Service.GetEventPrice(
                new GetEventPriceRequest { EventId = Guid.NewGuid().ToString() },
                _callContext
            )
        );

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateEventPrice_persists_the_new_pools()
    {
        var created = await CreateAsync();

        var response = await Service.UpdateEventPrice(
            UpdateRequest(created.Id, pot: 11m, poolYes: 0.5m, poolNo: 2m),
            _callContext
        );

        Assert.True(response.Success);
        await using var context = NewContext();
        var stored = await context.Events.SingleAsync(e => e.Id == created.Id);
        Assert.Equal(11m, stored.Pot);
        Assert.Equal(0.5m, stored.PoolYes);
        Assert.Equal(2m, stored.PoolNo);
    }

    [Fact]
    public async Task UpdateEventPrice_broadcasts_prices_derived_from_what_was_stored()
    {
        var created = await CreateAsync();

        await Service.UpdateEventPrice(
            UpdateRequest(created.Id, pot: 4m, poolYes: 1m, poolNo: 3m),
            _callContext
        );

        var update = Assert.Single(_publisher.Published);
        Assert.Equal(created.Id, update.EventId);
        Assert.Equal(0.75m, update.PriceYes);
        Assert.Equal(0.25m, update.PriceNo);
    }

    [Fact]
    public async Task A_settled_market_is_refused_and_nothing_is_broadcast()
    {
        var created = await CreateAsync();
        await Repository.ResolveEventAsync(new ResolveEventDto(created.Id, MarketOutcome.Yes));

        var response = await Service.UpdateEventPrice(
            UpdateRequest(created.Id, pot: 99m, poolYes: 9m, poolNo: 9m),
            _callContext
        );

        // Game turns this into an InvalidOperationException and refuses to approve the bet.
        Assert.False(response.Success);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task An_unknown_event_is_refused_and_nothing_is_broadcast()
    {
        var response = await Service.UpdateEventPrice(
            UpdateRequest(Guid.NewGuid(), pot: 1m, poolYes: 1m, poolNo: 1m),
            _callContext
        );

        Assert.False(response.Success);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task A_market_with_no_liquidity_is_stored_but_not_priced()
    {
        var created = await CreateAsync();

        var response = await Service.UpdateEventPrice(
            UpdateRequest(created.Id, pot: 0m, poolYes: 0m, poolNo: 0m),
            _callContext
        );

        // There is no meaningful price to send, and computing one would divide by zero.
        Assert.True(response.Success);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Pool_values_survive_the_round_trip_through_the_wire_format()
    {
        // This is the exact path a bet takes: Game formats 1/11 as a string, Catalog parses it,
        // stores it, and Game reads it back on the next bet.
        var created = await CreateAsync();
        var poolYes = 1m / 11m;

        await Service.UpdateEventPrice(
            UpdateRequest(created.Id, pot: 11m, poolYes: poolYes, poolNo: 11m),
            _callContext
        );

        var response = await Service.GetEventPrice(
            new GetEventPriceRequest { EventId = created.Id.ToString() },
            _callContext
        );

        Assert.Equal(
            poolYes,
            decimal.Parse(response.PoolYes, System.Globalization.CultureInfo.InvariantCulture)
        );
    }
}
