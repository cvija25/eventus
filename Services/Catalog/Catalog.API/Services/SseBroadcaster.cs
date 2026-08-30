using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace Catalog.API.Services;

public class SseBroadcaster : ISseBroadcaster
{
    private readonly ConcurrentDictionary<string, Channel<string>> _connections = new();

    public async Task SubscribeAsync(HttpResponse response, CancellationToken cancellationToken)
    {
        var connectionId = Guid.NewGuid().ToString("N");
        var channel = Channel.CreateUnbounded<string>();
        _connections[connectionId] = channel;

        response.Headers["Content-Type"] = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        using var pingTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        var pingTask = Task.Run(
            async () =>
            {
                try
                {
                    while (await pingTimer.WaitForNextTickAsync(cancellationToken))
                        channel.Writer.TryWrite(":ping\n\n");
                }
                catch (OperationCanceledException) { }
            },
            cancellationToken
        );

        try
        {
            await response.Body.FlushAsync(cancellationToken);

            await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
            {
                await response.WriteAsync(message, cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnected — normal
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
        }
    }

    public void PublishPriceUpdate(Guid eventId, decimal priceYes, decimal priceNo)
    {
        var payload = new
        {
            type = "price",
            eventId = eventId.ToString(),
            priceYes,
            priceNo,
        };

        var json = JsonSerializer.Serialize(payload);
        var message = $"data: {json}\n\n";

        foreach (var channel in _connections.Values)
            channel.Writer.TryWrite(message);
    }
}
