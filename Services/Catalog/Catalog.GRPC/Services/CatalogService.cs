using AutoMapper;
using Catalog.Common.DTOs;
using Catalog.Common.Repositories;
using Grpc.Core;

namespace Catalog.GRPC.Services;

public class CatalogService(
    ILogger<CatalogService> logger,
    IEventRepository eventRepository,
    IMapper mapper
) : Catalog.CatalogBase
{
    public override async Task<GetEventPriceResponse> GetEventPrice(
        GetEventPriceRequest request,
        ServerCallContext context
    )
    {
        logger.LogInformation("Received GetEventPrice");
        var eventId = Guid.Parse(request.EventId);
        var ev = await eventRepository.GetEventByIdAsync(eventId);
        if (ev is null)
        {
            throw new RpcException(
                new Status(StatusCode.NotFound, $"Event with id {eventId} not found")
            );
        }
        return mapper.Map<GetEventPriceResponse>(ev);
    }

    public override async Task<UpdateEventPriceResponse> UpdateEventPrice(
        UpdateEventPriceRequest request,
        ServerCallContext context
    )
    {
        logger.LogInformation("Received UpdateEventPrice");
        var succeeded = await eventRepository.UpdateEventAsync(mapper.Map<UpdateEventDto>(request));
        return new UpdateEventPriceResponse { Success = succeeded };
    }
}
