using Common.Messaging;

namespace Account.Publishers;

public interface IGameCommandPublisher
{
    public Task PublishBetPlacedAsync(BetPlacedEvent evt);
    public Task PublishSellSharesAsync(SellSharesEvent evt);
}
