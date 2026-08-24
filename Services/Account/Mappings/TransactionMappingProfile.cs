using Account.DTOs;
using Account.Entities;
using AutoMapper;

namespace Account.Mappings;

public class TransactionMappingProfile : Profile
{
    public TransactionMappingProfile()
    {
        CreateMap<Transaction, TransactionDTO>()
            .ForCtorParam(nameof(TransactionDTO.UserId), opt => opt.MapFrom(src => src.AccountId));
    }
}
