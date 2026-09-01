using Account.DTOs;
using Account.Publishers;
using Account.Repositories;
using Common.Enums;
using Common.Messaging;
using Common.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Account.Controllers;

[ApiController]
[Route("/api/v1/account")]
public class AccountController(
    IGameCommandPublisher publisher,
    IWalletRepository walletRepository,
    ITransactionRepository transactionRepository,
    ICurrentUser currentUser,
    ILogger<AccountController> logger
) : ControllerBase
{
    [HttpPost("sell")]
    [Authorize]
    public async Task<ActionResult<string>> PublishSellShares([FromBody] SellSharesRequest request)
    {
        if (request.Shares <= 0)
            return BadRequest("Shares must be greater than zero");

        if (!Enum.IsDefined(request.Outcome))
        {
            logger.LogWarning(
                "Invalid game outcome: Outcome={Outcome}, EventId={EventId}",
                request.Outcome,
                request.EventId
            );
            return BadRequest("Outcome must be Yes (1) or No (2)");
        }

        if (currentUser.UserId is not { } ownerId)
            return Unauthorized();

        var transactions = await transactionRepository.GetTransactionsForUser(ownerId);
        var availableShares = transactions
            .Where(transaction =>
                transaction.EventId == request.EventId && transaction.Outcome == request.Outcome
            )
            .Sum(transaction =>
                transaction.Type == TransactionType.Buy
                    ? transaction.ShareAmount
                    : -transaction.ShareAmount
            ); // shares already sold for event are removed

        if (availableShares < request.Shares)
            return Conflict("You do not own enough shares for this sale");

        logger.LogInformation(
            "Sell shares request received: AccountId={AccountId}, Shares={Shares}",
            ownerId,
            request.Shares
        );

        var evt = new SellSharesEvent
        {
            OwnerId = ownerId,
            EventId = request.EventId,
            Shares = request.Shares,
            Outcome = request.Outcome,
            ExpectedPrice = request.ExpectedPrice,
            SlippageDelta = request.SlippageDelta,
        };

        await publisher.PublishSellSharesAsync(evt);
        return Ok("Sell shares event published");
    }

    [HttpPost("buy")]
    [Authorize]
    public async Task<ActionResult<string>> PublishBet([FromBody] BetPlacedRequest request)
    {
        if (request.Stake <= 0)
        {
            logger.LogWarning(
                "Invalid buy stake: Stake={Stake}, EventId={EventId}",
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

        if (currentUser.UserId is not { } ownerId)
            return Unauthorized();

        var funds = await walletRepository.GetBalance(ownerId);
        if (funds == null)
            return BadRequest("User does not have a wallet. Try creating a new account.");

        logger.LogInformation(
            "Buy request received: AccountId={AccountId}, Stake={Stake}, CurrentBalance={CurrentBalance}",
            ownerId,
            request.Stake,
            funds.Amount
        );

        if (funds.Amount < request.Stake)
        {
            logger.LogWarning(
                "Insufficient funds for bet: AccountId={AccountId}, Stake={Stake}, CurrentBalance={CurrentBalance}",
                ownerId,
                request.Stake,
                funds.Amount
            );
            return Conflict("Not enough credits in account");
        }

        await walletRepository.DepositReserveFund(ownerId, request.Stake);

        var evt = new BetPlacedEvent
        {
            OwnerId = ownerId,
            EventId = request.EventId,
            Stake = request.Stake,
            Outcome = request.Outcome,
            ExpectedPrice = request.ExpectedPrice,
            SlippageDelta = request.SlippageDelta,
        };

        await publisher.PublishBetPlacedAsync(evt);
        return Ok("Buy event published");
    }

    [HttpGet("balance")]
    [Authorize]
    public async Task<ActionResult<object?>> GetBalance()
    {
        if (currentUser.UserId is not { } ownerId)
            return Unauthorized();

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
        if (currentUser.UserId is not { } ownerId)
            return Unauthorized();

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
        if (currentUser.UserId is not { } ownerId)
            return Unauthorized();

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

    [HttpPost("wallet")]
    public async Task<ActionResult<bool>> CreateWallet([FromBody] CreateWalletDto createWalletDto)
    {
        await walletRepository.CreateWalletIfMissing(createWalletDto.UserId);
        return NoContent();
    }
}
