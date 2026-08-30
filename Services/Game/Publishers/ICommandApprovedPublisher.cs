using Common.Messaging;

namespace Game.Publishers;

public interface ICommandApprovedPublisher
{
    public Task PublishBetApprovedAsync(BetApprovedEvent evt);
    public Task PublishSellSharesApprovedAsync(SellSharesApprovedEvent evt);
}
