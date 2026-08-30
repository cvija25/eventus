using System.Collections.Concurrent;
using System.Text;
using Common.Enums;
using Common.Messaging;
using Eventus.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Common.Tests;

[CollectionDefinition(Name)]
public class RabbitCollection : ICollectionFixture<RabbitMqFixture>
{
    public const string Name = "common-rabbitmq";
}

/// <summary>
/// The publish/consume base classes every service inherits from, against a real broker. They
/// carry the delivery guarantees the whole system leans on - nothing else verifies that an
/// unhandled message is retried rather than dropped, or that a malformed one is discarded
/// rather than poisoning the queue.
/// </summary>
[Collection(RabbitCollection.Name)]
public class MessageQueueTests(RabbitMqFixture rabbit) : IAsyncLifetime
{
    private readonly string _queue = $"test-{Guid.NewGuid():N}";
    private readonly List<IAsyncDisposable> _disposables = [];
    private ServiceProvider _services = null!;

    public Task InitializeAsync()
    {
        _services = new ServiceCollection().BuildServiceProvider();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            try
            {
                await disposable.DisposeAsync();
            }
            catch (ObjectDisposedException) { }
        }
        await _services.DisposeAsync();
    }

    private IOptions<RabbitMqOptions> Options() =>
        Microsoft.Extensions.Options.Options.Create(
            new RabbitMqOptions
            {
                HostName = rabbit.HostName,
                Port = rabbit.Port,
                UserName = RabbitMqFixture.UserName,
                Password = RabbitMqFixture.Password,
                VirtualHost = "/",
            }
        );

    private sealed class TestPublisher(IOptions<RabbitMqOptions> options, string queue)
        : MessageQueuePublisher(options, NullLogger.Instance)
    {
        // Initialised before the base constructor runs, which is where the queue is declared.
        protected override string QueueName { get; } = queue;

        public Task SendAsync<T>(string type, T message) => PublishAsync(type, message);
    }

    private sealed class TestConsumer(
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopes,
        string queue,
        Func<MessageEnvelope, Task> handle
    ) : MessageQueueConsumer(NullLogger.Instance, scopes, options)
    {
        protected override string QueueName { get; } = queue;

        protected override Task HandleAsync(
            MessageEnvelope envelope,
            IServiceProvider services,
            CancellationToken ct
        ) => handle(envelope);
    }

    private TestPublisher StartPublisher()
    {
        var publisher = new TestPublisher(Options(), _queue);
        _disposables.Add(publisher);
        return publisher;
    }

    private async Task<TestConsumer> StartConsumer(Func<MessageEnvelope, Task> handle)
    {
        var consumer = new TestConsumer(
            Options(),
            _services.GetRequiredService<IServiceScopeFactory>(),
            _queue,
            handle
        );
        await consumer.StartAsync(CancellationToken.None);
        return consumer;
    }

    [Fact]
    public async Task A_published_message_reaches_the_consumer_intact()
    {
        var received = new ConcurrentQueue<MessageEnvelope>();
        var consumer = await StartConsumer(e =>
        {
            received.Enqueue(e);
            return Task.CompletedTask;
        });

        var sent = new BetPlacedEvent
        {
            EventId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Stake = 12.5m,
            Outcome = MarketOutcome.No,
        };
        await StartPublisher().SendAsync(MessageTypes.BetPlaced, sent);

        await WaitFor.UntilAsync(() => Task.FromResult(!received.IsEmpty), "the message to arrive");
        Assert.True(received.TryDequeue(out var envelope));
        Assert.Equal(MessageTypes.BetPlaced, envelope!.Type);
        var payload = envelope.Deserialize<BetPlacedEvent>();
        Assert.Equal(sent.EventId, payload.EventId);
        Assert.Equal(12.5m, payload.Stake);
        Assert.Equal(MarketOutcome.No, payload.Outcome);

        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_message_published_before_anyone_listens_is_still_delivered()
    {
        // The queue is declared durable and the message persistent, so Account can publish a bet
        // while Game is still starting up without the command being lost.
        await StartPublisher().SendAsync(MessageTypes.BetPlaced, new BetPlacedEvent { Stake = 7m });

        var received = new ConcurrentQueue<MessageEnvelope>();
        var consumer = await StartConsumer(e =>
        {
            received.Enqueue(e);
            return Task.CompletedTask;
        });

        await WaitFor.UntilAsync(() => Task.FromResult(!received.IsEmpty), "the queued message");
        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_message_the_handler_fails_on_is_retried_rather_than_dropped()
    {
        var attempts = 0;
        var consumer = await StartConsumer(_ =>
        {
            // Fail once, then succeed - a transient database blip, say.
            if (Interlocked.Increment(ref attempts) == 1)
                throw new InvalidOperationException("transient failure");
            return Task.CompletedTask;
        });

        await StartPublisher().SendAsync(MessageTypes.BetPlaced, new BetPlacedEvent { Stake = 1m });

        // Losing a bet command would leave the user's funds held forever, so redelivery matters.
        // The consumer backs off for 5s before nacking, hence the generous window.
        await WaitFor.UntilAsync(
            () => Task.FromResult(Volatile.Read(ref attempts) >= 2),
            "the failed message to be redelivered",
            TimeSpan.FromSeconds(45)
        );

        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_malformed_message_is_discarded_instead_of_blocking_the_queue()
    {
        var received = new ConcurrentQueue<MessageEnvelope>();
        var consumer = await StartConsumer(e =>
        {
            received.Enqueue(e);
            return Task.CompletedTask;
        });

        await PublishRawAsync("this is not an envelope");
        await StartPublisher().SendAsync(MessageTypes.BetPlaced, new BetPlacedEvent { Stake = 3m });

        // The good message arriving proves the bad one was dropped rather than endlessly requeued.
        await WaitFor.UntilAsync(
            () => Task.FromResult(!received.IsEmpty),
            "the following valid message to be processed"
        );
        Assert.True(received.TryDequeue(out var envelope));
        Assert.Equal(MessageTypes.BetPlaced, envelope!.Type);

        await consumer.StopAsync(CancellationToken.None);
    }

    private async Task PublishRawAsync(string body)
    {
        var factory = new ConnectionFactory
        {
            HostName = rabbit.HostName,
            Port = rabbit.Port,
            UserName = RabbitMqFixture.UserName,
            Password = RabbitMqFixture.Password,
        };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(_queue, true, false, false);
        await channel.BasicPublishAsync(
            string.Empty,
            _queue,
            false,
            new BasicProperties { Persistent = true },
            Encoding.UTF8.GetBytes(body)
        );
    }

    [Fact]
    public async Task A_consumer_recovers_when_the_broker_drops_the_connection()
    {
        var received = new ConcurrentQueue<MessageEnvelope>();
        var consumer = await StartConsumer(e =>
        {
            received.Enqueue(e);
            return Task.CompletedTask;
        });
        await StartPublisher().SendAsync(MessageTypes.BetPlaced, new BetPlacedEvent { Stake = 1m });
        await WaitFor.UntilAsync(() => Task.FromResult(!received.IsEmpty), "the first message");

        await rabbit.DropAllConnectionsAsync();

        // A broker restart must not leave a service permanently deaf to its queue.
        var publisher = new TestPublisher(Options(), _queue);
        _disposables.Add(publisher);
        await WaitFor.UntilAsync(
            async () =>
            {
                received.Clear();
                await publisher.SendAsync(
                    MessageTypes.BetPlaced,
                    new BetPlacedEvent { Stake = 2m }
                );
                await Task.Delay(500);
                return !received.IsEmpty;
            },
            "the consumer to reconnect and resume",
            TimeSpan.FromSeconds(60)
        );

        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stopping_a_consumer_after_the_connection_dropped_does_not_throw()
    {
        var consumer = await StartConsumer(_ => Task.CompletedTask);
        await StartPublisher().SendAsync(MessageTypes.BetPlaced, new BetPlacedEvent { Stake = 1m });
        await Task.Delay(1000);

        await rabbit.DropAllConnectionsAsync();
        await Task.Delay(2000);

        // StopAsync closes the channel unconditionally. Once the broker has taken the connection
        // away the channel is already disposed, so an ordinary SIGTERM shutdown throws out of
        // IHostedService.StopAsync instead of shutting down cleanly.
        await consumer.StopAsync(CancellationToken.None);
    }
}
