using Catalog.Common.DTOs;
using Common.Enums;
using Eventus.Testing;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Integration;

public class EventRepositoryTests(PostgresFixture postgres) : CatalogDatabaseTest(postgres)
{
    private Task<EventDto> CreateAsync(string title = "Will it rain?", Guid? owner = null) =>
        Repository.CreateEventAsync(new CreateEventDto(title), owner ?? Guid.NewGuid());

    [Fact]
    public async Task A_new_event_is_seeded_with_a_balanced_market()
    {
        var owner = Guid.NewGuid();

        var created = await CreateAsync("Will it rain?", owner);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Will it rain?", created.Title);
        Assert.Equal(owner, created.OwnerId);
        Assert.Equal(1m, created.Pot);
        Assert.Equal(1m, created.PoolYes);
        Assert.Equal(1m, created.PoolNo);
        Assert.Equal(0.5m, created.PriceYes);
        Assert.Equal(0.5m, created.PriceNo);
        Assert.Null(created.Outcome);
    }

    [Fact]
    public async Task A_created_event_is_persisted()
    {
        var created = await CreateAsync();

        await using var context = NewContext();
        var stored = await context.Events.SingleAsync(e => e.Id == created.Id);
        Assert.Equal(created.Title, stored.Title);
        Assert.Equal(1m, stored.Pot);
    }

    [Fact]
    public async Task GetEventById_is_null_for_an_unknown_id()
    {
        Assert.Null(await Repository.GetEventByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetEvents_projects_prices_for_every_event()
    {
        await CreateAsync("first");
        var second = await CreateAsync("second");
        await Repository.UpdateEventAsync(new UpdateEventDto(second.Id, PoolYes: 1m, PoolNo: 3m));

        var events = await Repository.GetEventsAsync();

        Assert.Equal(2, events.Count);
        Assert.Equal(0.75m, events.Single(e => e.Id == second.Id).PriceYes);
        Assert.All(events, e => Assert.Equal(1m, e.PriceYes!.Value + e.PriceNo!.Value));
    }

    [Fact]
    public async Task UpdateEvent_writes_the_new_pools_through_to_the_database()
    {
        var created = await CreateAsync();

        var updated = await Repository.UpdateEventAsync(
            new UpdateEventDto(created.Id, Pot: 11m, PoolYes: 0.5m, PoolNo: 2m)
        );

        Assert.NotNull(updated);
        Assert.Equal(11m, updated.Pot);

        await using var context = NewContext();
        var stored = await context.Events.SingleAsync(e => e.Id == created.Id);
        Assert.Equal(11m, stored.Pot);
        Assert.Equal(0.5m, stored.PoolYes);
        Assert.Equal(2m, stored.PoolNo);
    }

    [Fact]
    public async Task UpdateEvent_leaves_out_what_the_caller_omitted()
    {
        var created = await CreateAsync();

        var updated = await Repository.UpdateEventAsync(new UpdateEventDto(created.Id, Pot: 5m));

        Assert.NotNull(updated);
        Assert.Equal(5m, updated.Pot);
        Assert.Equal(1m, updated.PoolYes);
        Assert.Equal(1m, updated.PoolNo);
        Assert.Equal(created.Title, updated.Title);
    }

    [Fact]
    public async Task UpdateEvent_is_null_for_an_unknown_id()
    {
        Assert.Null(await Repository.UpdateEventAsync(new UpdateEventDto(Guid.NewGuid(), Pot: 5m)));
    }

    [Fact]
    public async Task A_resolved_market_can_no_longer_be_moved()
    {
        var created = await CreateAsync();
        await Repository.ResolveEventAsync(new ResolveEventDto(created.Id, MarketOutcome.Yes));

        var updated = await Repository.UpdateEventAsync(new UpdateEventDto(created.Id, Pot: 99m));

        // Trading against a settled market would mint shares that can never pay out.
        Assert.Null(updated);
        await using var context = NewContext();
        Assert.Equal(1m, (await context.Events.SingleAsync(e => e.Id == created.Id)).Pot);
    }

    [Fact]
    public async Task ResolveEvent_records_the_outcome()
    {
        var created = await CreateAsync();

        Assert.True(
            await Repository.ResolveEventAsync(new ResolveEventDto(created.Id, MarketOutcome.No))
        );

        await using var context = NewContext();
        Assert.Equal(
            MarketOutcome.No,
            (await context.Events.SingleAsync(e => e.Id == created.Id)).Outcome
        );
    }

    [Fact]
    public async Task An_event_can_only_be_resolved_once()
    {
        var created = await CreateAsync();
        await Repository.ResolveEventAsync(new ResolveEventDto(created.Id, MarketOutcome.Yes));

        var second = await Repository.ResolveEventAsync(
            new ResolveEventDto(created.Id, MarketOutcome.No)
        );

        // Re-resolving would pay out a second time on the opposite outcome.
        Assert.False(second);
        await using var context = NewContext();
        Assert.Equal(
            MarketOutcome.Yes,
            (await context.Events.SingleAsync(e => e.Id == created.Id)).Outcome
        );
    }

    [Fact]
    public async Task ResolveEvent_is_false_for_an_unknown_id()
    {
        Assert.False(
            await Repository.ResolveEventAsync(
                new ResolveEventDto(Guid.NewGuid(), MarketOutcome.Yes)
            )
        );
    }

    [Fact]
    public async Task Pool_precision_survives_a_round_trip_through_postgres()
    {
        // Pools land on values like 1/11 after a bet. A float column would quietly round these
        // and break the constant-product invariant the market maker depends on.
        var created = await CreateAsync();
        var poolYes = 1m / 11m;

        await Repository.UpdateEventAsync(
            new UpdateEventDto(created.Id, PoolYes: poolYes, PoolNo: 11m)
        );

        await using var context = NewContext();
        Assert.Equal(poolYes, (await context.Events.SingleAsync(e => e.Id == created.Id)).PoolYes);
    }
}
