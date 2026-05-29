using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Account.Services;

public class BetApprovedConsumer(
    ILogger<BetApprovedConsumer> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> rabbitOptions) : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _channel;

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
                logger.LogError(ex, "RabbitMQ consumer failed. Retrying in 5s...");
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

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 10,
            global: false,
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
            queue: "bet-approved",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken
        );

        logger.LogInformation("RabbitMQ consumer started.");

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
        await using var scope = scopeFactory.CreateAsyncScope();
        // var handler = scope.ServiceProvider.GetRequiredService<IBetApprovedHandler>();

        try
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var evt = JsonSerializer.Deserialize<BetApprovedEvent>(json);

            logger.LogInformation("Bet approved: {BetId}", evt?.BetId);

            // TODO: await handler.HandleAsync(evt, CancellationToken.None);

            await _channel!.BasicAckAsync(
                deliveryTag: ea.DeliveryTag,
                multiple: false,
                cancellationToken: CancellationToken.None
            );
        }
        catch (JsonException ex)
        {
            // Bad message — don't requeue, send to dead letter
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
            // Transient failure — requeue with delay
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

public class BetApprovedEvent
{
    public Guid BetId { get; set; }
    public Guid UserId { get; set; }
    public decimal ApprovedStake { get; set; }
}