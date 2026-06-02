using System.Text;
using System.Text.Json;
using Contracts;
using Game.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Game.Publishers;

public class BetApprovedPublisher : IAsyncDisposable
{
    private readonly IChannel _channel;
    private readonly IConnection _connection;

    public BetApprovedPublisher(IOptions<RabbitMqOptions> options)
    {
        var rabbitOptions = options.Value;

        var factory = new ConnectionFactory
        {
            HostName = rabbitOptions.HostName,
            Port = rabbitOptions.Port,
            UserName = rabbitOptions.UserName,
            Password = rabbitOptions.Password
        };

        _connection = factory.CreateConnectionAsync().Result;
        _channel = _connection.CreateChannelAsync().Result;
        const string queue = "bet-approved";
        _channel.QueueDeclareAsync(
            queue,
            true,
            false,
            false
        ).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();

        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }

    public async Task PublishBetApprovedAsync(BetApprovedEvent evt)
    {
        var json = JsonSerializer.Serialize(evt);
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };
        const string queue = "bet-approved";
        await _channel.BasicPublishAsync(
            "",
            queue,
            false,
            props,
            body
        );
    }
}