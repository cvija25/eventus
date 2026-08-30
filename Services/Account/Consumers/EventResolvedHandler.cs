using Account.Data;
using Account.Repositories;
using Common.Messaging;

namespace Account.Consumers;

public class EventResolvedHandler(
    IWalletRepository walletRepository,
    ITransactionRepository transactionRepository,
    AccountDbContext dbContext
) : IEventResolvedHandler
{
    public async Task ProcessEventResolvedAsync(EventResolvedEvent evt, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var resolvedTransactions = await transactionRepository.GetTransactionsForEvent(
                evt.EventId
            );

            var winners = resolvedTransactions
                .Where(t => (int)t.Outcome == (int)evt.Outcome)
                .ToList();

            foreach (var tx in winners)
            {
                await walletRepository.Deposit(tx.UserId, tx.ShareAmount);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
