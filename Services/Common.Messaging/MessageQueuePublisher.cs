namespace Common.Messaging;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

public class MessageQueuePublisher : IAsyncDisposable
{
    private readonly IChannel _channel;
    private readonly IConnection _connection;

    public MessageQueuePublisher(IOptions<RabbitMqOptions> options, string queueName)
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

        _channel.QueueDeclareAsync(queueName, true, false, false).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();

        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
        ;
    }
}
