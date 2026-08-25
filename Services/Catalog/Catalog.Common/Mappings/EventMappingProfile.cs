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
            );
    }
}
