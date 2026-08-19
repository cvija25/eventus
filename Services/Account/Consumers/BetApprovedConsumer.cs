using System.Text;
using System.Text.Json;
using Account.Data;
using Account.DTOs;
using Account.Repositories;
using Contracts;
using Contracts.Messaging;
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

        await _channel.BasicQosAsync(0, 10, false, stoppingToken);

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
        var walletRepository = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var transactionRepository =
            scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var db = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var evt = JsonSerializer.Deserialize<BetApprovedEvent>(json);

            if (evt is null)
            {
                logger.LogWarning("Received null event, discarding.");
                await _channel!.BasicNackAsync(
                    ea.DeliveryTag,
                    false,
                    false,
                    CancellationToken.None
                );
                return;
            }

            if (evt.IsApproved)
            {
                await using var transaction = await db.Database.BeginTransactionAsync(
                    CancellationToken.None
                );
                try
                {
                    await walletRepository.Withdraw(evt.AccId, evt.Stake);
                    await transactionRepository.CreateTransaction(
                        new TransactionDTO(evt.EventId, evt.AccId, evt.ShareAmount, evt.Outcome)
                    );
                    await transaction.CommitAsync(CancellationToken.None);
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            }

            logger.LogInformation(
                "Bet processed: AccountId={AccountId} Amount={Amount} Approved={IsApproved}",
                evt.AccId,
                evt.Stake,
                evt.IsApproved
            );

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
