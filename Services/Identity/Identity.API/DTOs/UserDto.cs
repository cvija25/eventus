namespace Identity.API.DTOs;

public record UserDto(string Id, string Email, string Role, DateTime CreatedAt);
