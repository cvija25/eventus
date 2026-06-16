using System.IdentityModel.Tokens.Jwt;
using Account.DTOs;
using Account.Publishers;
using Contracts;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
    public async Task<ActionResult<string>> PublishBet([FromBody] BetPlacedRequest request)
    {
        var ownerId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

        var evt = new BetPlacedEvent
        {
            OwnerId = ownerId,
            EventId = request.EventId,
            Stake = request.Stake,
        };

        await _publisher.PublishBetPlacedAsync(evt);

        return Ok("Bet event published");
    }
}
