using Contracts;

namespace Catalog.API.Publishers;

public interface IEventResolvedPublisher
{
    public Task PublishEventResolvedAsync(EventResolvedEvent evt);
}