using System.Text.Json;
using Catalog.API.Services;
using Catalog.Common.Repositories;
using Confluent.Kafka;
using Contracts;
using Contracts.Messaging;
using Microsoft.Extensions.Options;

namespace Catalog.API.Consumers;

public class PriceUpdateConsumer(
    ILogger<PriceUpdateConsumer> logger,
    ISseBroadcaster broadcaster,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await StartConsuming(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Kafka consumer failed. Retrying in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
    }

    private async Task StartConsuming(CancellationToken stoppingToken)
    {
        var options = kafkaOptions.Value;

        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = "catalog-price-update",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(options.PriceUpdateTopic);

        logger.LogInformation("Kafka price update consumer started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume(stoppingToken);

            try
            {
                var envelope =
                    JsonSerializer.Deserialize<MessageEnvelope>(result.Message.Value)
                    ?? throw new JsonException("Failed to deserialize price update.");

                switch (envelope.Type)
                {
                    case MessageTypes.PriceUpdate:
                        await ProcessPriceUpdate(envelope.Deserialize<PriceUpdateEvent>());
                        break;
                    default:
                        throw new JsonException($"Unknown command result type '{envelope.Type}'.");
                }

                consumer.Commit(result);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Invalid message format. Skipping.");
                consumer.Commit(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process price update. Will retry on restart.");
            }
        }

        consumer.Close();
    }

    private async Task ProcessPriceUpdate(PriceUpdateEvent evt)
    {
        broadcaster.PublishPriceUpdate(evt.EventId, evt.PriceYes, evt.PriceNo);

        using var scope = scopeFactory.CreateScope();
        var historyRepository = scope.ServiceProvider.GetRequiredService<IPriceHistoryRepository>();
        await historyRepository.AddPrice(evt.EventId, evt.PriceYes, evt.PriceNo, DateTime.UtcNow);
    }
}
