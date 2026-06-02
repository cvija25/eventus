using Account.DTOs;
using Account.Publishers;
using Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Account.Controllers;

[ApiController]
[Route("/api/v1/account")]
public class AccountController : ControllerBase
{
    private readonly BetPlacedPublisher _publisher;

    public AccountController(BetPlacedPublisher publisher)
    {
        _publisher = publisher;
    }

    [HttpGet]
    public ActionResult<string> GetGreeting()
    {
        return Ok("Hello World, from Account!");
    }

    [HttpPost]
    public async Task<ActionResult<string>> PublishBet([FromBody] BetPlacedRequest request)
    {
        var evt = new BetPlacedEvent
        {
            BetId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Stake = request.Stake,
        };

        await _publisher.PublishBetPlacedAsync(evt);

        return Ok("Bet event published");
    }
}
