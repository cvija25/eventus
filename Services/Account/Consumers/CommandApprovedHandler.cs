using Account.Data;
using Account.DTOs;
using Account.Repositories;
using Common.Enums;
using Common.Messaging;

namespace Account.Consumers;

public class CommandApprovedHandler(
    IWalletRepository walletRepository,
    ITransactionRepository transactionRepository,
    AccountDbContext dbContext
) : ICommandApprovedHandler
{
    public async Task ProcessBetAsync(BetApprovedEvent evt, CancellationToken ct)
    {
        if (!evt.IsApproved)
        {
            await walletRepository.WithdrawReserveFund(evt.AccId, evt.Stake);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            await walletRepository.WithdrawReserveFund(evt.AccId, evt.Stake);
            await walletRepository.Withdraw(evt.AccId, evt.Stake);
            await transactionRepository.CreateTransaction(
                new TransactionDTO(
                    evt.EventId,
                    evt.AccId,
                    evt.ShareAmount,
                    evt.Outcome,
                    TransactionType.Buy
                )
            );
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task ProcessSaleAsync(SellSharesApprovedEvent evt, CancellationToken ct)
    {
        if (!evt.IsApproved)
            return;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            await walletRepository.Deposit(evt.AccId, evt.SellPrice);
            await transactionRepository.CreateTransaction(
                new TransactionDTO(
                    evt.EventId,
                    evt.AccId,
                    evt.ShareAmount,
                    evt.Outcome,
                    TransactionType.Sell
                )
            );
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
