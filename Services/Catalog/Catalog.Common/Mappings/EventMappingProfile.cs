using AutoMapper;
using Catalog.Common.DTOs;
using Catalog.Common.Entities;

namespace Catalog.Common.Mappings;

public class EventMappingProfile : Profile
{
    public EventMappingProfile()
    {
        // A market with no liquidity has no meaningful price; report 0 rather than
        // dividing by zero (decimal division by zero throws, unlike double).
        CreateMap<Event, EventDto>()
            .ForMember(
                dest => dest.PriceYes,
                opt =>
                    opt.MapFrom(src =>
                        src.PoolYes + src.PoolNo == 0m
                            ? 0m
                            : src.PoolNo / (src.PoolYes + src.PoolNo)
                    )
            )
            .ForMember(
                dest => dest.PriceNo,
                opt =>
                    opt.MapFrom(src =>
                        src.PoolYes + src.PoolNo == 0m
                            ? 0m
                            : src.PoolYes / (src.PoolYes + src.PoolNo)
                    )
            );
        CreateMap<PriceHistory, PriceHistoryDto>();
    }
}
