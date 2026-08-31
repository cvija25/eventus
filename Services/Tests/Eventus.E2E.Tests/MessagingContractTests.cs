using Account.Consumers;
using Account.Publishers;
using Catalog.API.Consumers;
using Catalog.API.Publishers;
using Catalog.GRPC.Publishers;
using Common.Enums;
using Common.Messaging;
using Contracts.Messaging;
using Eventus.Testing;
using Game.Consumers;
using Game.Publishers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Eventus.E2E.Tests;

/// <summary>
/// Every message the system sends, from the real publisher that emits it to the real consumer
/// that reads it, over a real broker.
/// <para>
/// Each pairing is an agreement between two services on two values - a queue name and a message
/// type - declared in four separate files with nothing forcing them to match. Change the queue
/// on one side and the publisher succeeds, the consumer waits forever, and the failure surfaces
/// as bets that silently never settle. These tests are what make that a build failure.
/// </para>
/// <para>
/// This lives in the end-to-end project because it is the only one referencing all five
/// services; it needs a broker but none of the databases, so it uses RabbitMQ on its own rather
/// than the full <see cref="EventusFixture"/>.
/// </para>
/// </summary>
public class MessagingContractTests(RabbitMqFixture rabbit, KafkaFixture kafka)
    : IClassFixture<RabbitMqFixture>,
        IClassFixture<KafkaFixture>,
        IAsyncLifetime
{
    private readonly List<IAsyncDisposable> _disposables = [];

    public Task InitializeAsync() => Task.CompletedTask;

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
    }

    private IOptions<KafkaOptions> KafkaSettings() =>
        Microsoft.Extensions.Options.Options.Create(
            new KafkaOptions
            {
                BootstrapServers = kafka.BootstrapServers,
                PriceUpdateTopic = KafkaConstants.PriceUpdateTopic,
            }
        );

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

    /// <summary>A substituted handler plus the scope factory a consumer resolves it through.</summary>
    private (IServiceScopeFactory Scopes, THandler Handler) HandlerFor<THandler>()
        where THandler : class
    {
        var handler = Substitute.For<THandler>();
        var provider = new ServiceCollection().AddSingleton(handler).BuildServiceProvider();
        _disposables.Add(provider);
        return (provider.GetRequiredService<IServiceScopeFactory>(), handler);
    }

    private async Task Delivered<THandler>(THandler handler, string what)
        where THandler : class =>
        await WaitFor.UntilAsync(
            () => Task.FromResult(handler.ReceivedCalls().Any()),
            what,
            TimeSpan.FromSeconds(30)
        );

    private T Track<T>(T disposable)
        where T : IAsyncDisposable
    {
        _disposables.Add(disposable);
        return disposable;
    }

    // ---- Account -> Game: the command queue ------------------------------------------

    [Fact]
    public async Task A_bet_placed_by_Account_is_received_by_Game()
    {
        var (scopes, handler) = HandlerFor<IGameCommandHandler>();
        var consumer = new GameCommandConsumer(
            NullLogger<GameCommandConsumer>.Instance,
            Options(),
            scopes
        );
        await consumer.StartAsync(CancellationToken.None);

        var evt = new BetPlacedEvent
        {
            EventId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Stake = 12.5m,
            Outcome = MarketOutcome.No,
        };
        await Track(new GameCommandPublisher(Options(), NullLogger<GameCommandPublisher>.Instance))
            .PublishBetPlacedAsync(evt);

        await Delivered(handler, "Game to receive the placed bet");
        await handler
            .Received(1)
            .ProcessBetPlacedAsync(
                Arg.Is<BetPlacedEvent>(e =>
                    e.EventId == evt.EventId && e.Stake == 12.5m && e.Outcome == MarketOutcome.No
                )
            );

        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_sale_requested_by_Account_is_received_by_Game()
    {
        var (scopes, handler) = HandlerFor<IGameCommandHandler>();
        var consumer = new GameCommandConsumer(
            NullLogger<GameCommandConsumer>.Instance,
            Options(),
            scopes
        );
        await consumer.StartAsync(CancellationToken.None);

        var evt = new SellSharesEvent
        {
            EventId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Shares = 4m,
            Outcome = MarketOutcome.Yes,
        };
        await Track(new GameCommandPublisher(Options(), NullLogger<GameCommandPublisher>.Instance))
            .PublishSellSharesAsync(evt);

        await Delivered(handler, "Game to receive the sale request");
        await handler
            .Received(1)
            .ProcessSellSharesAsync(
                Arg.Is<SellSharesEvent>(e => e.EventId == evt.EventId && e.Shares == 4m)
            );

        await consumer.StopAsync(CancellationToken.None);
    }

    // ---- Game -> Account: the approval queue -----------------------------------------

    [Fact]
    public async Task A_bet_approved_by_Game_is_received_by_Account()
    {
        var (scopes, handler) = HandlerFor<ICommandApprovedHandler>();
        var consumer = new CommandApprovedConsumer(
            NullLogger<CommandApprovedConsumer>.Instance,
            scopes,
            Options()
        );
        await consumer.StartAsync(CancellationToken.None);

        var evt = new BetApprovedEvent
        {
            AccId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Stake = 10m,
            ShareAmount = 19m,
            IsApproved = true,
            Outcome = MarketOutcome.Yes,
        };
        await Track(
                new CommandApprovedPublisher(
                    Options(),
                    NullLogger<CommandApprovedPublisher>.Instance
                )
            )
            .PublishBetApprovedAsync(evt);

        await Delivered(handler, "Account to receive the approved bet");
        await handler
            .Received(1)
            .ProcessBetAsync(
                Arg.Is<BetApprovedEvent>(e => e.AccId == evt.AccId && e.ShareAmount == 19m),
                Arg.Any<CancellationToken>()
            );

        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_sale_approved_by_Game_is_received_by_Account()
    {
        var (scopes, handler) = HandlerFor<ICommandApprovedHandler>();
        var consumer = new CommandApprovedConsumer(
            NullLogger<CommandApprovedConsumer>.Instance,
            scopes,
            Options()
        );
        await consumer.StartAsync(CancellationToken.None);

        var evt = new SellSharesApprovedEvent
        {
            AccId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            SellPrice = 10m,
            ShareAmount = 20m,
            IsApproved = true,
            Outcome = MarketOutcome.No,
        };
        await Track(
                new CommandApprovedPublisher(
                    Options(),
                    NullLogger<CommandApprovedPublisher>.Instance
                )
            )
            .PublishSellSharesApprovedAsync(evt);

        await Delivered(handler, "Account to receive the approved sale");
        await handler
            .Received(1)
            .ProcessSaleAsync(
                Arg.Is<SellSharesApprovedEvent>(e => e.AccId == evt.AccId && e.SellPrice == 10m),
                Arg.Any<CancellationToken>()
            );

        await consumer.StopAsync(CancellationToken.None);
    }

    // ---- Catalog.API -> Account: the settlement queue --------------------------------

    [Fact]
    public async Task An_event_resolved_by_Catalog_is_received_by_Account()
    {
        var (scopes, handler) = HandlerFor<IEventResolvedHandler>();
        var consumer = new EventResolvedEventConsumer(
            NullLogger<EventResolvedEventConsumer>.Instance,
            scopes,
            Options()
        );
        await consumer.StartAsync(CancellationToken.None);

        var evt = new EventResolvedEvent { EventId = Guid.NewGuid(), Outcome = MarketOutcome.No };
        await Track(
                new EventResolvedPublisher(Options(), NullLogger<EventResolvedPublisher>.Instance)
            )
            .PublishEventResolvedAsync(evt);

        await Delivered(handler, "Account to receive the resolved event");
        await handler
            .Received(1)
            .ProcessEventResolvedAsync(
                Arg.Is<EventResolvedEvent>(e =>
                    e.EventId == evt.EventId && e.Outcome == MarketOutcome.No
                ),
                Arg.Any<CancellationToken>()
            );

        await consumer.StopAsync(CancellationToken.None);
    }

    // ---- Catalog.GRPC -> Catalog.API: the price topic --------------------------------

    [Fact]
    public async Task A_price_update_published_by_the_grpc_host_is_received_by_the_api()
    {
        // This is the one flow that runs over Kafka rather than RabbitMQ, so it is also the one
        // where publisher and consumer have to agree on a topic name instead of a queue name.
        var (scopes, handler) = HandlerFor<IPriceUpdateHandler>();
        var consumer = new PriceUpdateConsumer(
            NullLogger<PriceUpdateConsumer>.Instance,
            scopes,
            KafkaSettings()
        );
        await consumer.StartAsync(CancellationToken.None);

        var evt = new PriceUpdateEvent
        {
            EventId = Guid.NewGuid(),
            PriceYes = 0.75m,
            PriceNo = 0.25m,
        };
        await Track(
                new PriceUpdatePublisher(KafkaSettings(), NullLogger<PriceUpdatePublisher>.Instance)
            )
            .PublishPriceUpdateAsync(evt);

        // Kafka has to create the topic and assign the group a partition first, which is slower
        // to get going than a RabbitMQ queue.
        await WaitFor.UntilAsync(
            () => Task.FromResult(handler.ReceivedCalls().Any()),
            "the API to receive the price update",
            TimeSpan.FromSeconds(90)
        );
        handler
            .Received(1)
            .ProcessPriceUpdateAsync(
                Arg.Is<PriceUpdateEvent>(e => e.EventId == evt.EventId && e.PriceYes == 0.75m)
            );

        await consumer.StopAsync(CancellationToken.None);
    }
}
