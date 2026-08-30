using Microsoft.Extensions.Logging;

namespace Common.Messaging;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

public abstract class MessageQueuePublisher : IAsyncDisposable
{
    private readonly IChannel _channel;
    private readonly IConnection _connection;
    private readonly ILogger _logger;
    protected abstract string QueueName { get; }

    protected MessageQueuePublisher(IOptions<RabbitMqOptions> options, ILogger logger)
    {
        _logger = logger;
        var rabbitOptions = options.Value;

        var factory = new ConnectionFactory
        {
            HostName = rabbitOptions.HostName,
            Port = rabbitOptions.Port,
            UserName = rabbitOptions.UserName,
            Password = rabbitOptions.Password,
        };

        _connection = factory.CreateConnectionAsync().Result;
        _channel = _connection.CreateChannelAsync().Result;

        _channel.QueueDeclareAsync(QueueName, true, false, false).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();

        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }

    protected async Task PublishAsync<T>(string type, T evt)
    {
        var json = JsonSerializer.Serialize(MessageEnvelope.Create(type, evt));
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties { Persistent = true, ContentType = "application/json" };
        await _channel.BasicPublishAsync("", QueueName, false, props, body);
    }
}
