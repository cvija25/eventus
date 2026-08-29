using AutoMapper;
using Catalog.Common.DTOs;
using Catalog.Common.Repositories;
using Catalog.GRPC.Publishers;
using Common.Enums;
using Grpc.Core;

namespace Catalog.GRPC.Services;

public class CatalogService(
    PriceUpdatePublisher publisher,
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
        var dto = mapper.Map<UpdateEventDto>(request);
        var succeeded = await eventRepository.UpdateEventAsync(dto);
        var poolYes = dto.PoolYes.HasValue ? dto.PoolYes.Value : 0;
        ;
        var poolNo = dto.PoolNo.HasValue ? dto.PoolNo.Value : 0;
        var evt = new PriceUpdateEvent
        {
            EventId = dto.Id,
            PriceYes = poolNo / (poolYes + poolNo),
            PriceNo = poolYes / (poolYes + poolNo),
        };
        await publisher.PublishPriceUpdateAsync(evt);
        return new UpdateEventPriceResponse { Success = succeeded };
    }
}
