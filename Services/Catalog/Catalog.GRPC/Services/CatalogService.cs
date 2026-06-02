using AutoMapper;
using Catalog.Common.Repositories;
using Grpc.Core;

namespace Catalog.GRPC.Services;

public class CatalogService(ILogger<CatalogService> logger, IEventRepository eventRepository, IMapper mapper)
    : Catalog.CatalogBase
{
    public override async Task<GetEventPriceResponse> GetEventPrice(GetEventPriceRequest request,
        ServerCallContext context)
    {
        logger.LogInformation("Received GetEventPrice");
        var eventId = Guid.Parse(request.EventId);
        var ev = await eventRepository.GetEventByIdAsync(eventId);
        return ev is null ? throw new RpcException(new Status(StatusCode.NotFound, $"Event with id {eventId} not found")) : mapper.Map<GetEventPriceResponse>(ev);
    }
}