using AutoMapper;
using Catalog.Common.DTOs;
using Catalog.Common.Entities;

namespace Catalog.Common.Mappings;

public class EventMappingProfile : Profile
{
    public EventMappingProfile()
    {
        CreateMap<Event, EventDto>();
    }
}
