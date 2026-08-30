using Eventus.Testing;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Integration;

/// <summary>
/// The price history behind the detail-screen chart. Every point is written by the Kafka price
/// consumer on the trade path, so a failure here is a failure while settling somebody's bet.
/// </summary>
public class PriceHistoryRepositoryTests(PostgresFixture postgres) : CatalogDatabaseTest(postgres)
{
    [Fact]
    public async Task A_recorded_price_is_persisted()
    {
        var eventId = Guid.NewGuid();
        var at = DateTime.UtcNow;

        var added = await History.AddPrice(eventId, 0.75m, 0.25m, at);

        Assert.Equal(eventId, added.EventId);
        Assert.Equal(0.75m, added.PriceYes);

        await using var context = NewHistoryContext();
        var stored = await context.Histories.SingleAsync(h => h.EventId == eventId);
        Assert.Equal(0.75m, stored.PriceYes);
        Assert.Equal(0.25m, stored.PriceNo);
    }

    [Fact]
    public async Task History_comes_back_oldest_first()
    {
        var eventId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddMinutes(-10);
        await History.AddPrice(eventId, 0.60m, 0.40m, start.AddMinutes(2));
        await History.AddPrice(eventId, 0.50m, 0.50m, start);
        await History.AddPrice(eventId, 0.70m, 0.30m, start.AddMinutes(5));

        var history = await History.GetHistory(eventId);

        // The chart plots these in order; unordered points would draw a scribble.
        Assert.Equal([0.50m, 0.60m, 0.70m], history.Select(h => h.PriceYes));
    }

    [Fact]
    public async Task History_is_scoped_to_one_event()
    {
        var mine = Guid.NewGuid();
        await History.AddPrice(mine, 0.5m, 0.5m, DateTime.UtcNow);
        await History.AddPrice(Guid.NewGuid(), 0.9m, 0.1m, DateTime.UtcNow);

        var history = await History.GetHistory(mine);

        Assert.Equal(mine, Assert.Single(history).EventId);
    }

    [Fact]
    public async Task An_event_that_never_traded_has_an_empty_history()
    {
        Assert.Empty(await History.GetHistory(Guid.NewGuid()));
    }

    [Fact]
    public async Task Price_precision_survives_a_round_trip()
    {
        var eventId = Guid.NewGuid();
        var priceYes = 1m / 3m;

        await History.AddPrice(eventId, priceYes, 1m - priceYes, DateTime.UtcNow);

        Assert.Equal(priceYes, Assert.Single(await History.GetHistory(eventId)).PriceYes);
    }
}
