using AutoMapper;
using Catalog.Common.DTOs;
using Catalog.Common.Entities;

namespace Catalog.Common.Mappings;

public class EventMappingProfile : Profile
{
    public EventMappingProfile()
    {
        CreateMap<Event, EventDto>()
            .ForMember(
                dest => dest.PriceYes,
                opt => opt.MapFrom(src => src.PoolNo / (src.PoolYes + src.PoolNo))
            )
            .ForMember(
                dest => dest.PriceNo,
                opt => opt.MapFrom(src => src.PoolYes / (src.PoolYes + src.PoolNo))
            );
        CreateMap<PriceHistory, PriceHistoryDto>();
    }
}
