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

public class BetPlacedConsumer(
    ILogger<BetPlacedConsumer> logger,
    IOptions<RabbitMqOptions> rabbitOptions,
    IServiceScopeFactory scopeFactory
) : BackgroundService
{
    private IChannel? _channel;
    private IConnection? _connection;
    private BetPlacedHandler? _handler;
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
                logger.LogError(ex, "BetPlacedConsumer failed. Retrying in 5s...");
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
        var publisherLogger = loggerFactory.CreateLogger<BetApprovedPublisher>();
        _handler = new BetPlacedHandler(new BetApprovedPublisher(rabbitOptions, publisherLogger), catalogClient);

        await _channel.BasicQosAsync(0, 10, false, stoppingToken);

        await _channel.QueueDeclareAsync(
            RabbitMQConstants.BetPlacedQueue,
            true,
            false,
            false,
            cancellationToken: stoppingToken
        );

        await _channel.QueueDeclareAsync(
            RabbitMQConstants.BetApprovedQueue,
            true,
            false,
            false,
            cancellationToken: stoppingToken
        );

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        await _channel.BasicConsumeAsync(
            RabbitMQConstants.BetPlacedQueue,
            false,
            consumer,
            stoppingToken
        );

        logger.LogInformation("BetPlacedConsumer started.");

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
            var betPlaced =
                JsonSerializer.Deserialize<BetPlacedEvent>(json)
                ?? throw new JsonException("Failed to deserialize BetPlacedEvent");

            logger.LogInformation(
                "Received bet: {EventId}, {OwnerId}, {Stake}",
                betPlaced.EventId,
                betPlaced.OwnerId,
                betPlaced.Stake
            );

            await _handler!.ProcessBetPlacedAsync(betPlaced);

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
