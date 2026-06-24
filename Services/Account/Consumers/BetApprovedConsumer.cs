using System.Text;
using System.Text.Json;
using Contracts.Messaging;
using Contracts;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Account.Consumers;

public class BetApprovedConsumer(
    ILogger<BetApprovedConsumer> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> rabbitOptions
) : BackgroundService
{
    private IChannel? _channel;
    private IConnection? _connection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
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

        const int prefetchCount = 10,
            prefetchSize = 0;
        await _channel.BasicQosAsync(prefetchSize, prefetchCount, false, stoppingToken);

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
            RabbitMQConstants.BetApprovedQueue,
            false,
            consumer,
            stoppingToken
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
        try
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var evt = JsonSerializer.Deserialize<BetApprovedEvent>(json);

            logger.LogInformation("Bet approved: {IsApproved}", evt?.IsApproved);

            await _channel!.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
        }
        catch (JsonException ex)
        {
            // Bad message — don't requeue, send to dead letter
            logger.LogError(ex, "Invalid message format. Discarding.");
            await _channel!.BasicNackAsync(ea.DeliveryTag, false, false, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Transient failure — requeue with delay
            logger.LogError(ex, "Failed to process message. Requeuing.");
            await Task.Delay(TimeSpan.FromSeconds(5));
            await _channel!.BasicNackAsync(ea.DeliveryTag, false, true, CancellationToken.None);
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
