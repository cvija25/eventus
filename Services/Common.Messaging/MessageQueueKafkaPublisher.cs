using System.Text.Json;
using Confluent.Kafka;
using Contracts.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Common.Messaging;

public abstract class MessageQueueKafkaPublisher : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly IProducer<string, string> _producer;

    protected MessageQueueKafkaPublisher(IOptions<KafkaOptions> options, ILogger logger)
    {
        _logger = logger;
        var kafkaOptions = options.Value;

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    protected abstract string TopicName { get; }

    public ValueTask DisposeAsync()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        return ValueTask.CompletedTask;
    }

    protected async Task PublishAsync<T>(string type, T evt, string key)
    {
        var json = JsonSerializer.Serialize(MessageEnvelope.Create(type, evt));

        try
        {
            var result = await _producer.ProduceAsync(
                TopicName,
                new Message<string, string> { Key = key, Value = json }
            );

            _logger.LogInformation(
                "Published to Kafka: Topic={Topic}, Partition={Partition}, Offset={Offset}, Type={Type}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value,
                type
            );
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish to Kafka: Topic={Topic}, Type={Type}, Reason={Reason}",
                TopicName,
                type,
                ex.Error.Reason
            );
            throw;
        }
    }
}
