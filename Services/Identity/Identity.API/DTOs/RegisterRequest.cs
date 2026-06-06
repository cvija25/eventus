namespace Identity.API.DTOs;

public record RegisterRequest(string Email, string Password, bool IsAdmin);
public record RegisterResponse(string Id, string Email, string Role);
