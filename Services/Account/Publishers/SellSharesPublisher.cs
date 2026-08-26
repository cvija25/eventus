using System.Text;
using System.Text.Json;
using Contracts;
using Contracts.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Account.Publishers;

public class SellSharesPublisher : IAsyncDisposable
{
    private readonly IChannel _channel;
    private readonly IConnection _connection;

    public SellSharesPublisher(IOptions<RabbitMqOptions> options)
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
            .QueueDeclareAsync(RabbitMQConstants.GameCommandQueue, true, false, false)
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

    public async Task PublishSellSharesAsync(SellSharesEvent evt)
    {
        var json = JsonSerializer.Serialize(MessageEnvelope.Create(MessageTypes.SellShares, evt));
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties { Persistent = true, ContentType = "application/json" };

        await _channel.BasicPublishAsync(
            "",
            RabbitMQConstants.GameCommandQueue,
            false,
            props,
            body
        );
    }
}
