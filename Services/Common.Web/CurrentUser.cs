using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;

namespace Common.Web;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(
            httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            out var userId
        )
            ? userId
            : null;
}
