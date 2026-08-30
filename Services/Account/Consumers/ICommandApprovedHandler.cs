using Common.Messaging;

namespace Account.Consumers;

public interface ICommandApprovedHandler
{
    Task ProcessBetAsync(BetApprovedEvent evt, CancellationToken ct);
    Task ProcessSaleAsync(SellSharesApprovedEvent evt, CancellationToken ct);
}
