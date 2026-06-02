using System.Text;
using System.Text.Json;
using Account.Messaging;
using Contracts;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Account.Publishers;

public class BetPlacedPublisher : IAsyncDisposable
{
    private readonly IChannel _channel;
    private readonly IConnection _connection;

    public BetPlacedPublisher(IOptions<RabbitMqOptions> options)
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

        _channel.QueueDeclareAsync("bet-placed", true, false, false).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();

        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }

    public async Task PublishBetPlacedAsync(BetPlacedEvent evt)
    {
        var json = JsonSerializer.Serialize(evt);
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties { Persistent = true, ContentType = "application/json" };

        await _channel.BasicPublishAsync("", "bet-placed", false, props, body);
    }
}
