using AutoMapper;
using Identity.API.DTOs;
using Identity.API.Entities;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Npgsql;

namespace Identity.API.Repositories;

public class UserRepository(IMongoCollection<User> users, IMapper mapper, IConfiguration configuration)
    : IUserRepository
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
        await CreateWalletAsync(user.Id);
        return mapper.Map<UserDto>(user);
    }

    private async Task CreateWalletAsync(Guid userId)
    {
        var connectionString = configuration.GetConnectionString("AccountDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("AccountDb connection string is not configured.");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "INSERT INTO \"Wallets\" (\"AccId\", \"Amount\") VALUES (@id, 0) ON CONFLICT (\"AccId\") DO NOTHING",
            connection
        );
        command.Parameters.AddWithValue("id", userId);
        await command.ExecuteNonQueryAsync();
    }
}
