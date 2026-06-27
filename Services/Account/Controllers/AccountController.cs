using System.IdentityModel.Tokens.Jwt;
using Account.DTOs;
using Account.Publishers;
using Account.Repositories;
using Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Account.Controllers;

[ApiController]
[Route("/api/v1/account")]
public class AccountController(BetPlacedPublisher publisher, IWalletRepository walletRepository)
    : ControllerBase
{
    [HttpGet]
    public ActionResult<string> GetGreeting()
    {
        return Ok("Hello World, from Account!");
    }

    [HttpPost("bet")]
    [Authorize]
    public async Task<ActionResult<string>> PublishBet([FromBody] BetPlacedRequest request)
    {
        var ownerId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

        var evt = new BetPlacedEvent
        {
            OwnerId = ownerId,
            EventId = request.EventId,
            Stake = request.Stake
        };

        await publisher.PublishBetPlacedAsync(evt);
        return Ok("Bet event published");
    }

    [HttpPost("deposit")]
    [Authorize]
    public async Task<ActionResult<UpdateWalletDto?>> DepositCoins([FromBody] DepositRequest request)
    {
        var ownerId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var result = await walletRepository.Deposit(ownerId, request.Amount);

        if (result is null) return NotFound("Wallet not found");

        return Ok(result);
    }
}