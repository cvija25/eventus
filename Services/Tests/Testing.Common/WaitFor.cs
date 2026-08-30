using System.Diagnostics;

namespace Eventus.Testing;

/// <summary>
/// Polling helpers for effects that arrive asynchronously (a RabbitMQ consumer committing a
/// database write, say). Polling to a deadline keeps the tests fast when the broker is quick
/// and stable when it is not; a fixed sleep would be both slower and flakier.
/// </summary>
public static class WaitFor
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public static async Task UntilAsync(
        Func<Task<bool>> condition,
        string description,
        TimeSpan? timeout = null
    )
    {
        var deadline = Stopwatch.StartNew();
        var limit = timeout ?? DefaultTimeout;

        while (deadline.Elapsed < limit)
        {
            if (await condition())
                return;
            await Task.Delay(PollInterval);
        }

        throw new TimeoutException(
            $"Timed out after {limit.TotalSeconds:0}s waiting for: {description}"
        );
    }

    /// <summary>Polls until <paramref name="read"/> returns a non-null value, then returns it.</summary>
    public static async Task<T> ValueAsync<T>(
        Func<Task<T?>> read,
        string description,
        TimeSpan? timeout = null
    )
        where T : class
    {
        T? result = null;
        await UntilAsync(async () => (result = await read()) is not null, description, timeout);
        return result!;
    }
}
