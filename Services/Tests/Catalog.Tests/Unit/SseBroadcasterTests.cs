using System.Reflection;
using System.Text;
using System.Text.Json;
using Catalog.API.Consumers;
using Catalog.API.Services;
using Common.Messaging;
using Eventus.Testing;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Catalog.Tests.Unit;

public class SseBroadcasterTests
{
    /// <summary>
    /// A subscriber wired to an in-memory body. SubscribeAsync registers the connection
    /// synchronously before its first await, so a publish issued after this call is guaranteed
    /// to be seen.
    /// </summary>
    private sealed class Subscriber : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly MemoryStream _body = new();
        public Task Pump { get; }

        public Subscriber(ISseBroadcaster broadcaster)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = _body;
            Response = context.Response;
            Pump = broadcaster.SubscribeAsync(context.Response, _cts.Token);
        }

        public HttpResponse Response { get; }

        public async Task<string> AwaitFrameAsync()
        {
            await WaitFor.UntilAsync(
                () => Task.FromResult(_body.Length > 0),
                "the subscriber to receive a frame",
                TimeSpan.FromSeconds(5)
            );
            await StopAsync();
            return Text();
        }

        public async Task StopAsync()
        {
            await _cts.CancelAsync();
            await Pump;
        }

        public string Text() => Encoding.UTF8.GetString(_body.ToArray());

        public void Dispose()
        {
            _cts.Dispose();
            _body.Dispose();
        }
    }

    [Fact]
    public async Task Sets_the_headers_that_keep_an_event_stream_unbuffered()
    {
        var broadcaster = new SseBroadcaster();
        using var subscriber = new Subscriber(broadcaster);

        Assert.Equal("text/event-stream", subscriber.Response.Headers["Content-Type"]);
        Assert.Equal("no-cache", subscriber.Response.Headers["Cache-Control"]);
        // Without this, an nginx in front of the API buffers the stream and no price ever lands.
        Assert.Equal("no", subscriber.Response.Headers["X-Accel-Buffering"]);

        await subscriber.StopAsync();
    }

    [Fact]
    public async Task Delivers_a_price_update_as_a_single_sse_frame()
    {
        var broadcaster = new SseBroadcaster();
        using var subscriber = new Subscriber(broadcaster);
        var eventId = Guid.NewGuid();

        broadcaster.PublishPriceUpdate(eventId, 0.4m, 0.6m);
        var frame = await subscriber.AwaitFrameAsync();

        Assert.StartsWith("data: ", frame);
        Assert.EndsWith("\n\n", frame);

        using var payload = JsonDocument.Parse(frame["data: ".Length..].Trim());
        var root = payload.RootElement;
        Assert.Equal("price", root.GetProperty("type").GetString());
        Assert.Equal(eventId.ToString(), root.GetProperty("eventId").GetString());
        Assert.Equal(0.4m, root.GetProperty("priceYes").GetDecimal());
        Assert.Equal(0.6m, root.GetProperty("priceNo").GetDecimal());
    }

    [Fact]
    public async Task Fans_every_update_out_to_all_subscribers()
    {
        var broadcaster = new SseBroadcaster();
        using var first = new Subscriber(broadcaster);
        using var second = new Subscriber(broadcaster);

        broadcaster.PublishPriceUpdate(Guid.NewGuid(), 0.5m, 0.5m);

        Assert.Contains("\"type\":\"price\"", await first.AwaitFrameAsync());
        Assert.Contains("\"type\":\"price\"", await second.AwaitFrameAsync());
    }

    /// <summary>
    /// Reads the private connection map. SseBroadcaster exposes no count, and the alternative -
    /// adding one purely for tests - puts production surface in the way. A rename breaks this
    /// loudly rather than letting it pass silently, which is the right direction for the risk.
    /// </summary>
    private static int ConnectionCount(SseBroadcaster broadcaster)
    {
        var field =
            typeof(SseBroadcaster).GetField(
                "_connections",
                BindingFlags.NonPublic | BindingFlags.Instance
            )
            ?? throw new InvalidOperationException(
                "SseBroadcaster._connections was renamed; update this helper."
            );

        return ((System.Collections.ICollection)field.GetValue(broadcaster)!).Count;
    }

    [Fact]
    public async Task A_disconnected_subscriber_is_removed_rather_than_leaked()
    {
        var broadcaster = new SseBroadcaster();

        var stale = new Subscriber(broadcaster);
        Assert.Equal(1, ConnectionCount(broadcaster));

        await stale.StopAsync();
        stale.Dispose();

        // Each connection is an unbounded channel that PublishPriceUpdate writes to on every
        // trade, plus a keep-alive every 15s. A disconnect that failed to remove its entry would
        // leave that channel growing for the lifetime of the process, so every browser tab that
        // ever opened the stream would cost memory forever.
        Assert.Equal(0, ConnectionCount(broadcaster));

        using var live = new Subscriber(broadcaster);
        Assert.Equal(1, ConnectionCount(broadcaster));

        broadcaster.PublishPriceUpdate(Guid.NewGuid(), 0.5m, 0.5m);

        Assert.Contains("\"type\":\"price\"", await live.AwaitFrameAsync());
        Assert.Equal(0, ConnectionCount(broadcaster));
    }

    [Fact]
    public void Publishing_with_no_subscribers_does_not_throw()
    {
        var broadcaster = new SseBroadcaster();

        // Price updates arrive on every trade whether or not a browser is streaming. A throw
        // here would propagate out of PriceUpdateConsumer, which nacks with requeue after a 5s
        // delay - an endless retry loop over a message that can never succeed.
        Assert.Null(
            Record.Exception(() => broadcaster.PublishPriceUpdate(Guid.NewGuid(), 0.5m, 0.5m))
        );
    }

    [Fact]
    public void PriceUpdateHandler_forwards_the_message_to_the_broadcaster()
    {
        var broadcaster = Substitute.For<ISseBroadcaster>();
        var handler = new PriceUpdateHandler(broadcaster);
        var evt = new PriceUpdateEvent
        {
            EventId = Guid.NewGuid(),
            PriceYes = 0.7m,
            PriceNo = 0.3m,
        };

        handler.ProcessPriceUpdate(evt);

        broadcaster.Received(1).PublishPriceUpdate(evt.EventId, 0.7m, 0.3m);
    }
}
