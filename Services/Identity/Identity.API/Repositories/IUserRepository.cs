using Identity.API.DTOs;
using Identity.API.Entities;

namespace Identity.API.Repositories;

public interface IUserRepository
{
    Task<UserDto?> FindByEmailAsync(string email);
    Task<User?> FindUserWithHashByEmailAsync(string email);
    Task<UserDto> CreateUserAsync(RegisterRequest request);
}
