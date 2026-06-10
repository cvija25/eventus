using AutoMapper;
using Identity.API.DTOs;
using Identity.API.Entities;

namespace Identity.API.Mappings;

public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()));
    }
}
