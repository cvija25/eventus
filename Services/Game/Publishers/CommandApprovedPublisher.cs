using System.Text;
using System.Text.Json;
using Contracts;
using Contracts.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Game.Publishers;

public class CommandApprovedPublisher : IAsyncDisposable
{
    private readonly IChannel _channel;
    private readonly IConnection _connection;
    private readonly ILogger<CommandApprovedPublisher> _logger;

    public CommandApprovedPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<CommandApprovedPublisher> logger
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
            .QueueDeclareAsync(RabbitMQConstants.CommandApprovedQueue, true, false, false)
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
        await PublishAsync(MessageTypes.BetApproved, evt);
    }

    public async Task PublishSellSharesApprovedAsync(SellSharesApprovedEvent evt)
    {
        await PublishAsync(MessageTypes.SellSharesApproved, evt);
    }
    
    public async Task PublishPriceChangedAsync(PriceChangedEvent evt)
    {
        await PublishAsync(MessageTypes.PriceChanged, evt);
    }

    private async Task PublishAsync<T>(string type, T evt)
    {
        var json = JsonSerializer.Serialize(MessageEnvelope.Create(type, evt));
        var body = Encoding.UTF8.GetBytes(json);

        _logger.LogInformation("Publishing command result: Type={Type}, Json={Json}", type, json);

        var props = new BasicProperties { Persistent = true, ContentType = "application/json" };
        await _channel.BasicPublishAsync(
            "",
            RabbitMQConstants.CommandApprovedQueue,
            false,
            props,
            body
        );
    }
}
