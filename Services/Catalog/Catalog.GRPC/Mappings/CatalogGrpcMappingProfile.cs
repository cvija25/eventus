using System.Globalization;
using AutoMapper;
using Catalog.Common.DTOs;

namespace Catalog.GRPC.Mappings;

/// <summary>
/// Translates between the wire contract and the Catalog DTOs. Decimals cross the gRPC boundary
/// as strings because protobuf has no decimal type.
/// </summary>
public class CatalogGrpcMappingProfile : Profile
{
    public CatalogGrpcMappingProfile()
    {
        CreateMap<EventDto, GetEventPriceResponse>().ReverseMap();

        CreateMap<UpdateEventPriceRequest, UpdateEventDto>()
            .ConstructUsing(src => new UpdateEventDto(
                Guid.Parse(src.EventId),
                decimal.Parse(src.Pot, CultureInfo.InvariantCulture),
                decimal.Parse(src.PoolYes, CultureInfo.InvariantCulture),
                decimal.Parse(src.PoolNo, CultureInfo.InvariantCulture)
            ))
            .ForAllMembers(opt => opt.Ignore());
    }
}
