using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using Account.Services;

namespace Account.Controllers;

[ApiController]
[Route("/api/v1/account")]
public class AccountController : ControllerBase
{
    private readonly RabbitMqPublisher _publisher;

    public AccountController(RabbitMqPublisher publisher)
    {
        _publisher = publisher;
    }
    [HttpGet]
    public ActionResult<string> GetGreeting()
    {
        return Ok("Hello World, from Account!");
    }

    [HttpPost]
    public async Task<ActionResult<string>> PublishBet()
    {
        var evt = new BetPlacedEvent
        {
            BetId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Stake = 100
        };

        await _publisher.PublishBetPlacedAsync(evt);

        return Ok("Bet event published");
    }

    public class RabbitMqPublisher : IAsyncDisposable
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;

        public RabbitMqPublisher(IOptions<RabbitMqOptions> options)
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

            _channel.QueueDeclareAsync(
                queue: "bet-placed",
                durable: true,
                exclusive: false,
                autoDelete: false
            ).GetAwaiter().GetResult();
        }

        public async Task PublishBetPlacedAsync(BetPlacedEvent evt)
        {
            var json = JsonSerializer.Serialize(evt);
            var body = Encoding.UTF8.GetBytes(json);

            var props = new BasicProperties
            {
                Persistent = true,          
                ContentType = "application/json"
            };

            await _channel.BasicPublishAsync(
                exchange: "",
                routingKey: "bet-placed",
                mandatory: false,
                basicProperties: props,
                body: body
            );
        }

        public async ValueTask DisposeAsync()
        {
            await _channel.CloseAsync();
            await _connection.CloseAsync();

            await _channel.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    public class BetPlacedEvent
    {
        public Guid BetId { get; set; }
        public Guid UserId { get; set; }
        public decimal Stake { get; set; }
    }
}