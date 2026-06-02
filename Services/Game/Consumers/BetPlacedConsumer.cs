using Game.Handlers;
using Game.Messaging;
using Game.Publishers;

namespace Game.Consumers;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Contracts;
using Microsoft.Extensions.Options;

public class BetPlacedConsumer(ILogger<BetPlacedConsumer> logger, IOptions<RabbitMqOptions> rabbitOptions) : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _channel;
    private BetPlacedHandler? _handler;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
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

            VirtualHost = options.VirtualHost

        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        _handler = new BetPlacedHandler(new BetApprovedPublisher(rabbitOptions));

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 10,
            global: false,
            cancellationToken: stoppingToken
        );

        await _channel.QueueDeclareAsync(
            queue: "bet-placed",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken
        );

        await _channel.QueueDeclareAsync(
            queue: "bet-approved",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken
        );

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        await _channel.BasicConsumeAsync(
            queue: "bet-placed",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken
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

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var betPlaced = JsonSerializer.Deserialize<BetPlacedEvent>(json);

            logger.LogInformation("Received bet: {BetId}", betPlaced?.BetId);
            _handler?.ProcessBetPlacedAsync(betPlaced);
            
            await _channel!.BasicAckAsync(
                deliveryTag: ea.DeliveryTag,
                multiple: false,
                cancellationToken: CancellationToken.None
            );
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid message format. Discarding.");
            await _channel!.BasicNackAsync(
                deliveryTag: ea.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process message. Requeuing.");
            await Task.Delay(TimeSpan.FromSeconds(5));
            await _channel!.BasicNackAsync(
                deliveryTag: ea.DeliveryTag,
                multiple: false,
                requeue: true,
                cancellationToken: CancellationToken.None
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

        await base.StopAsync(cancellationToken);
    }
}
