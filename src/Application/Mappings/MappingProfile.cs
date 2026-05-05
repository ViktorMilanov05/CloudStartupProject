using Application.DTOs;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.Role, opt => opt.MapFrom(s => s.Role.ToString()))
            .ForMember(d => d.CompanyName, opt => opt.MapFrom(s => s.Company != null ? s.Company.Name : null));
    }
}
