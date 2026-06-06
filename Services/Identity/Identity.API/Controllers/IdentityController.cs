using Identity.API.DTOs;
using Identity.API.Repositories;
using Identity.API.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Identity.API.Controllers;

[ApiController]
[Route("/api/v1/identity")]
public class IdentityController(IUserRepository userRepository, JwtService jwtService)
    : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var user = await userRepository.CreateUserAsync(request);
            return CreatedAtAction(
                nameof(Register),
                new RegisterResponse(user.Id, user.Email, user.Role)
            );
        }
        catch (MongoWriteException ex) when (ex.WriteError.Code == 11000)
        {
            return Conflict(new { message = "Email already registered." });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await userRepository.FindUserWithHashByEmailAsync(request.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        var token = jwtService.GenerateToken(
            new UserDto(user.Id.ToString(), user.Email, user.Role, user.CreatedAt)
        );

        return Ok(new LoginResponse(token));
    }
}
