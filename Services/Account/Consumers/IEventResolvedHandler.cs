using Common.Messaging;

namespace Account.Consumers;

public interface IEventResolvedHandler
{
    Task ProcessEventResolvedAsync(EventResolvedEvent evt, CancellationToken ct);
}
