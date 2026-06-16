namespace Identity.API.DTOs;

public record UserDto(string Id, string Name, string Email, string Role, DateTime CreatedAt);
