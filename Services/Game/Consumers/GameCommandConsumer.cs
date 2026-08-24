using System.Text;
using System.Text.Json;
using Contracts;
using Contracts.Messaging;
using Game.GrpcClients;
using Game.Handlers;
using Game.Publishers;
using Grpc.Core;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Game.Consumers;

public class GameCommandConsumer(
    ILogger<GameCommandConsumer> logger,
    IOptions<RabbitMqOptions> rabbitOptions,
    IServiceScopeFactory scopeFactory
) : BackgroundService
{
    private IChannel? _channel;
    private IConnection? _connection;
    private BetPlacedHandler? _betHandler;
    private SellSharesHandler? _sellSharesHandler;
    private IServiceScope? _scope;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await StartConsumingAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Game command consumer failed. Retrying in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
    }

    private async Task StartConsumingAsync(CancellationToken stoppingToken)
    {
        var options = rabbitOptions.Value;

        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        _scope?.Dispose();
        _scope = scopeFactory.CreateScope();
        var catalogClient = _scope.ServiceProvider.GetRequiredService<CatalogGrpcClient>();
        var loggerFactory = _scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var publisherLogger = loggerFactory.CreateLogger<CommandApprovedPublisher>();
        var publisher = new CommandApprovedPublisher(rabbitOptions, publisherLogger);
        _betHandler = new BetPlacedHandler(
            publisher,
            catalogClient,
            loggerFactory.CreateLogger<BetPlacedHandler>()
        );
        _sellSharesHandler = new SellSharesHandler(
            publisher,
            catalogClient,
            loggerFactory.CreateLogger<SellSharesHandler>()
        );

        await _channel.BasicQosAsync(0, 10, false, stoppingToken);

        await _channel.QueueDeclareAsync(
            RabbitMQConstants.GameCommandQueue,
            true,
            false,
            false,
            cancellationToken: stoppingToken
        );

        await _channel.QueueDeclareAsync(
            RabbitMQConstants.CommandApprovedQueue,
            true,
            false,
            false,
            cancellationToken: stoppingToken
        );

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        await _channel.BasicConsumeAsync(
            RabbitMQConstants.GameCommandQueue,
            false,
            consumer,
            stoppingToken
        );

        logger.LogInformation("Game command consumer started.");

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _connection.ConnectionShutdownAsync += (_, args) =>
        {
            logger.LogWarning("RabbitMQ connection lost: {Reason}", args.ReplyText);
            tcs.TrySetResult();
            return Task.CompletedTask;
        };

        stoppingToken.Register(() => tcs.TrySetResult());

        await tcs.Task;
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        try
        {
            var body = eventArgs.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var command = JsonSerializer.Deserialize<MessageEnvelope>(json)
                ?? throw new JsonException("Failed to deserialize game command.");

            switch (command.Type)
            {
                case MessageTypes.BetPlaced:
                    await _betHandler!.ProcessBetPlacedAsync(command.Deserialize<BetPlacedEvent>());
                    break;
                case MessageTypes.SellShares:
                    await _sellSharesHandler!.ProcessSellSharesAsync(
                        command.Deserialize<SellSharesEvent>()
                    );
                    break;
                default:
                    throw new JsonException($"Unknown game command type '{command.Type}'.");
            }

            await _channel!.BasicAckAsync(eventArgs.DeliveryTag, false, CancellationToken.None);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid message format. Discarding.");
            await _channel!.BasicNackAsync(
                eventArgs.DeliveryTag,
                false,
                false,
                CancellationToken.None
            );
        }
        catch (RpcException ex)
            when (ex.StatusCode is StatusCode.NotFound or StatusCode.InvalidArgument)
        {
            logger.LogError(ex, "Non-retryable catalog error. Discarding.");
            await _channel!.BasicNackAsync(
                eventArgs.DeliveryTag,
                false,
                false,
                CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process message. Requeuing.");
            await Task.Delay(TimeSpan.FromSeconds(5));
            await _channel!.BasicNackAsync(
                eventArgs.DeliveryTag,
                false,
                true,
                CancellationToken.None
            );
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync(cancellationToken);
            await _connection.DisposeAsync();
        }

        _scope?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
