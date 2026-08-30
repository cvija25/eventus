using System.Text.Json;
using Confluent.Kafka;
using Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Common.Messaging;

public abstract class MessageQueueKafkaConsumer(
    ILogger logger,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions
) : BackgroundService
{
    protected abstract string TopicName { get; }
    protected abstract string GroupId { get; }
    protected abstract Task HandleAsync(
        MessageEnvelope envelope,
        IServiceProvider services,
        CancellationToken ct
    );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await StartConsumingAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Kafka consumer failed. Retrying in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
    }

    private async Task StartConsumingAsync(CancellationToken stoppingToken)
    {
        var options = kafkaOptions.Value;

        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(TopicName);

        logger.LogInformation("Kafka consumer started for topic {Topic}.", TopicName);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (result is null)
                continue;

            try
            {
                var envelope =
                    JsonSerializer.Deserialize<MessageEnvelope>(result.Message.Value)
                    ?? throw new JsonException("Failed to deserialize message.");

                await using var scope = scopeFactory.CreateAsyncScope();
                await HandleAsync(envelope, scope.ServiceProvider, stoppingToken);

                consumer.Commit(result);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Invalid message format. Skipping.");
                consumer.Commit(result);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to process message. Will retry on restart. Offset={Offset}",
                    result.Offset
                );
                // Don't commit — message will be redelivered from this offset on restart
            }
        }

        consumer.Close();
    }
}
