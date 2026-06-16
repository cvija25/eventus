using AutoMapper;
using Identity.API.DTOs;
using Identity.API.Entities;
using MongoDB.Driver;

namespace Identity.API.Repositories;

public class UserRepository(IMongoCollection<User> users, IMapper mapper) : IUserRepository
{
    public async Task<UserDto?> FindByEmailAsync(string email)
    {
        var user = await users.Find(u => u.Email == email).FirstOrDefaultAsync();
        return user is null ? null : mapper.Map<UserDto>(user);
    }

    public Task<User?> FindUserWithHashByEmailAsync(string email) =>
        users.Find(u => u.Email == email).FirstOrDefaultAsync()!;

    public async Task<UserDto> CreateUserAsync(RegisterRequest request)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.IsAdmin ? "admin" : "user",
            CreatedAt = DateTime.UtcNow,
        };

        await users.InsertOneAsync(user);
        return mapper.Map<UserDto>(user);
    }
}
