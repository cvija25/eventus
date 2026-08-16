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
                dest => dest.PotSize,
                opt => opt.MapFrom(src => src.PotSizeNo + src.PotSizeYes)
            )
            .ForMember(
                dest => dest.PriceYes,
                opt => opt.MapFrom(src => src.PotSizeYes / (src.PotSizeYes + src.PotSizeNo))
            )
            .ForMember(
                dest => dest.PriceNo,
                opt => opt.MapFrom(src => src.PotSizeNo / (src.PotSizeYes + src.PotSizeNo))
            );
    }
}
