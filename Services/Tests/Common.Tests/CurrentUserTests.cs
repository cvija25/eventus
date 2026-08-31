using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Common.Web;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Common.Tests;

public class CurrentUserTests
{
    private static ICurrentUser CurrentUserFor(HttpContext? context)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        return new CurrentUser(accessor);
    }

    private static HttpContext ContextWith(params Claim[] claims)
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")),
        };
    }

    [Fact]
    public void Reads_the_user_id_from_the_sub_claim()
    {
        var userId = Guid.NewGuid();
        var currentUser = CurrentUserFor(
            ContextWith(new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()))
        );

        Assert.Equal(userId, currentUser.UserId);
    }

    [Fact]
    public void Is_null_when_there_is_no_http_context()
    {
        Assert.Null(CurrentUserFor(null).UserId);
    }

    [Fact]
    public void Is_null_for_an_unauthenticated_request()
    {
        Assert.Null(CurrentUserFor(ContextWith()).UserId);
    }

    [Fact]
    public void Is_null_when_the_sub_claim_is_not_a_guid()
    {
        var currentUser = CurrentUserFor(
            ContextWith(new Claim(JwtRegisteredClaimNames.Sub, "not-a-guid"))
        );

        Assert.Null(currentUser.UserId);
    }

    [Fact]
    public void Ignores_the_mapped_nameidentifier_claim()
    {
        // The services set MapInboundClaims = false so that "sub" survives as "sub". If that
        // ever flips back on, JwtBearer rewrites it to ClaimTypes.NameIdentifier and every
        // [Authorize] endpoint starts seeing a null user instead of failing loudly.
        var currentUser = CurrentUserFor(
            ContextWith(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()))
        );

        Assert.Null(currentUser.UserId);
    }
}
