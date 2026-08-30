using Common.Messaging;

namespace Game.Consumers;

public interface IGameCommandHandler
{
    public Task ProcessBetPlacedAsync(BetPlacedEvent betPlaced);
    public Task ProcessSellSharesAsync(SellSharesEvent sellShares);
}
