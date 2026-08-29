using System.Text;
using System.Text.Json;
using Common.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Catalog.API.Publishers;

public class EventResolvedPublisher : IEventResolvedPublisher
{
    private readonly IChannel _channel;
    private readonly IConnection _connection;

    public EventResolvedPublisher(IOptions<RabbitMqOptions> options)
    {
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

        _channel
            .QueueDeclareAsync(RabbitMQConstants.EventResolvedQueue, true, false, false)
            .GetAwaiter()
            .GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();

        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }

    public async Task PublishEventResolvedAsync(EventResolvedEvent evt)
    {
        var json = JsonSerializer.Serialize(evt);
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties { Persistent = true, ContentType = "application/json" };

        await _channel.BasicPublishAsync(
            "",
            RabbitMQConstants.EventResolvedQueue,
            false,
            props,
            body
        );
    }
}
