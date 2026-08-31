using AutoMapper;
using Catalog.Common.DTOs;
using Catalog.Common.Repositories;
using Catalog.GRPC.Publishers;
using Common.Enums;
using Common.Messaging;
using Grpc.Core;

namespace Catalog.GRPC.Services;

public class CatalogService(
    IPriceUpdatePublisher publisher,
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
        var updated = await eventRepository.UpdateEventAsync(dto);

        if (updated is null)
        {
            logger.LogWarning(
                "UpdateEventPrice rejected: EventId={EventId} not found or already resolved",
                dto.Id
            );
            return new UpdateEventPriceResponse { Success = false };
        }


        if (updated.PoolYes + updated.PoolNo == 0m)
        {
            logger.LogWarning(
                "Skipping price broadcast: EventId={EventId} has no liquidity",
                updated.Id
            );
        }
        else
        {
            await publisher.PublishPriceUpdateAsync(
                new PriceUpdateEvent
                {
                    EventId = updated.Id,
                    PriceYes = updated.PriceYes ?? 0m,
                    PriceNo = updated.PriceNo ?? 0m,
                }
            );
        }

        return new UpdateEventPriceResponse { Success = true };
    }
}
