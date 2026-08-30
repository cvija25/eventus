using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using Contracts.Messaging;
using Microsoft.Extensions.Options;

namespace Catalog.GRPC.Publishers;

public class PriceUpdatePublisher : IAsyncDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<PriceUpdatePublisher> _logger;
    private readonly string _topic;

    public PriceUpdatePublisher(
        IOptions<KafkaOptions> options,
        ILogger<PriceUpdatePublisher> logger
    )
    {
        _logger = logger;
        var kafkaOptions = options.Value;
        _topic = kafkaOptions.PriceUpdateTopic;

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public ValueTask DisposeAsync()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        return ValueTask.CompletedTask;
    }

    public async Task PublishPriceUpdateAsync(PriceUpdateEvent evt)
    {
        await PublishAsync(MessageTypes.PriceUpdate, evt, evt.EventId.ToString());
    }

    private async Task PublishAsync<T>(string type, T evt, string key)
    {
        var json = JsonSerializer.Serialize(MessageEnvelope.Create(type, evt));

        _logger.LogInformation("Publishing command result: Type={Type}, Json={Json}", type, json);

        try
        {
            var result = await _producer.ProduceAsync(
                _topic,
                new Message<string, string> { Key = key, Value = json }
            );

            _logger.LogInformation(
                "Delivered to Kafka: Topic={Topic}, Partition={Partition}, Offset={Offset}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value
            );
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish to Kafka: Type={Type}, Reason={Reason}",
                type,
                ex.Error.Reason
            );
            throw;
        }
    }
}
