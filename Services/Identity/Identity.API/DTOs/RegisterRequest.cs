namespace Identity.API.DTOs;

public record RegisterRequest(string Name, string Email, string Password, bool IsAdmin);

public record RegisterResponse(string Id, string Name, string Email, string Role);
