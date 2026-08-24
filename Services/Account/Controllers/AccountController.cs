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
public class AccountController(
    BetPlacedPublisher publisher,
    IWalletRepository walletRepository,
    ITransactionRepository transactionRepository,
    ILogger<AccountController> logger
) : ControllerBase
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
        if (request.Stake <= 0)
        {
            logger.LogWarning(
                "Invalid bet stake: Stake={Stake}, EventId={EventId}",
                request.Stake,
                request.EventId
            );
            return BadRequest("Stake must be greater than zero");
        }

        if (!Enum.IsDefined(request.Outcome))
        {
            logger.LogWarning(
                "Invalid bet outcome: Outcome={Outcome}, EventId={EventId}",
                request.Outcome,
                request.EventId
            );
            return BadRequest("Outcome must be Yes (1) or No (2)");
        }

        var ownerId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var funds = await walletRepository.GetBalance(ownerId);

        logger.LogInformation(
            "Bet request received: AccountId={AccountId}, Stake={Stake}, CurrentBalance={CurrentBalance}",
            ownerId,
            request.Stake,
            funds?.Amount ?? 0m
        );

        if (funds != null && funds.Amount < request.Stake)
        {
            logger.LogWarning(
                "Insufficient funds for bet: AccountId={AccountId}, Stake={Stake}, CurrentBalance={CurrentBalance}",
                ownerId,
                request.Stake,
                funds.Amount
            );
            return Conflict("Not enough credits in account");
        }

        var evt = new BetPlacedEvent
        {
            OwnerId = ownerId,
            EventId = request.EventId,
            Stake = request.Stake,
            Outcome = request.Outcome,
        };

        await publisher.PublishBetPlacedAsync(evt);
        return Ok("Bet event published");
    }

    [HttpGet("balance")]
    [Authorize]
    public async Task<ActionResult<object?>> GetBalance()
    {
        var ownerId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var result = await walletRepository.GetBalance(ownerId);

        logger.LogInformation(
            "Balance requested: AccountId={AccountId}, Amount={Amount}",
            ownerId,
            result?.Amount ?? 0m
        );

        if (result is null)
            return NotFound("Wallet not found");

        return Ok(new { amount = result.Amount });
    }

    [HttpGet("transactions")]
    [Authorize]
    public async Task<ActionResult<object?>> GetTransactions()
    {
        var ownerId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var result = await transactionRepository.GetTransactionsForUser(ownerId);
        logger.LogInformation("Transactions requested: AccountId={AccountId}", ownerId);

        if (result is null)
            return NotFound("Transactions not found");

        return Ok(result);
    }

    [HttpPost("deposit")]
    [Authorize]
    public async Task<ActionResult<UpdateWalletDto?>> DepositCoins(
        [FromBody] DepositRequest request
    )
    {
        var ownerId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        logger.LogInformation(
            "Deposit requested: AccountId={AccountId}, Amount={Amount}",
            ownerId,
            request.Amount
        );
        var result = await walletRepository.Deposit(ownerId, request.Amount);

        if (result is null)
            return NotFound("Wallet not found");

        logger.LogInformation(
            "Deposit completed: AccountId={AccountId}, NewBalance={NewBalance}",
            ownerId,
            result.Amount
        );

        return Ok(result);
    }
}
