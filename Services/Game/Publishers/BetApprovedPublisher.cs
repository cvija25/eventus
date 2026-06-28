using System.Text;
using System.Text.Json;
using Contracts;
using Contracts.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Game.Publishers;

public class BetApprovedPublisher : IAsyncDisposable
{
    private readonly IChannel _channel;
    private readonly IConnection _connection;
    private readonly ILogger<BetApprovedPublisher> _logger;

    public BetApprovedPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<BetApprovedPublisher> logger
    )
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
        _channel
            .QueueDeclareAsync(RabbitMQConstants.BetApprovedQueue, true, false, false)
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

    public async Task PublishBetApprovedAsync(BetApprovedEvent evt)
    {
        var json = JsonSerializer.Serialize(evt);
        var body = Encoding.UTF8.GetBytes(json);

        _logger.LogInformation(
            "Publishing BetApprovedEvent: AccId={AccId}, Stake={Stake}, IsApproved={IsApproved}, Json={Json}",
            evt.AccId,
            evt.Stake,
            evt.IsApproved,
            json
        );

        var props = new BasicProperties { Persistent = true, ContentType = "application/json" };
        await _channel.BasicPublishAsync(
            "",
            RabbitMQConstants.BetApprovedQueue,
            false,
            props,
            body
        );
    }
}
