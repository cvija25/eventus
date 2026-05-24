using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Account.Controllers;

[ApiController]
[Route("/api/v1/account")]
public class AccountController : ControllerBase
{
    [HttpGet]
    public ActionResult<string> GetGreeting()
    {
        return Ok("Hello World, from Account!");
    }

    [HttpPost]
    public async Task<ActionResult<string>> PublishBet()
    {
        await using var publisher = await RabbitMqPublisher.CreateAsync();

        var evt = new BetPlacedEvent
        {
            BetId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Stake = 100
        };

        await publisher.PublishBetPlacedAsync(evt);

        return Ok("Bet event published");
    }

    public class RabbitMqPublisher : IAsyncDisposable
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;

        private RabbitMqPublisher(IConnection connection, IChannel channel)
        {
            _connection = connection;
            _channel = channel;
        }

        public static async Task<RabbitMqPublisher> CreateAsync()
        {
            var factory = new ConnectionFactory
            {
                HostName = "rabbitmq",
                UserName = "guest",
                Password = "guest"
            };

            var connection = await factory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "bet-placed",
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            return new RabbitMqPublisher(connection, channel);
        }

        public async Task PublishBetPlacedAsync(BetPlacedEvent evt)
        {
            var json = JsonSerializer.Serialize(evt);
            var body = Encoding.UTF8.GetBytes(json);

            // ✅ In 7.x, BasicProperties is a struct passed directly — no CreateBasicProperties()
            var props = new BasicProperties
            {
                Persistent = true,          // ✅ This alone sets DeliveryMode = 2 internally
                ContentType = "application/json"
            };

            await _channel.BasicPublishAsync(
                exchange: "",
                routingKey: "bet-placed",
                mandatory: false,
                basicProperties: props,     // ✅ Accepts BasicProperties struct directly
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