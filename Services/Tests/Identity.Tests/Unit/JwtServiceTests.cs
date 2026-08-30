using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Common.Web;
using Identity.API.DTOs;
using Identity.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

namespace Identity.Tests.Unit;

public class JwtServiceTests
{
    private const string Secret = "super-secret-key-change-in-production-32chars!!";
    private const string Issuer = "eventus-identity";
    private const string Audience = "eventus-services";

    private static JwtService ServiceWith(params (string Key, string Value)[] overrides)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = Secret,
            ["Jwt:Issuer"] = Issuer,
            ["Jwt:Audience"] = Audience,
            ["Jwt:ExpiryMinutes"] = "60",
        };
        foreach (var (key, value) in overrides)
            settings[key] = value;

        return new JwtService(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    private static UserDto User(Guid? id = null, string role = "user") =>
        new((id ?? Guid.NewGuid()).ToString(), "Ada", "ada@example.com", role, DateTime.UtcNow);

    private static JwtSecurityToken Read(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    /// <summary>
    /// The validation parameters Account and Catalog.API configure, kept in one place so the
    /// contract tests below check the token against what the consuming services actually do.
    /// </summary>
    private static TokenValidationParameters ServiceValidationParameters() =>
        new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

    [Fact]
    public void Token_carries_the_identity_claims()
    {
        var userId = Guid.NewGuid();

        var token = Read(ServiceWith().GenerateToken(User(userId, "admin")));

        Assert.Equal(
            userId.ToString(),
            token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value
        );
        Assert.Equal("Ada", token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Name).Value);
        Assert.Equal(
            "ada@example.com",
            token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value
        );
        Assert.Equal("admin", token.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void Every_token_gets_a_unique_jti()
    {
        var service = ServiceWith();
        var user = User();

        var first = Read(service.GenerateToken(user))
            .Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti)
            .Value;
        var second = Read(service.GenerateToken(user))
            .Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti)
            .Value;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Token_is_stamped_with_the_configured_issuer_and_audience()
    {
        var token = Read(ServiceWith().GenerateToken(User()));

        Assert.Equal(Issuer, token.Issuer);
        Assert.Equal(Audience, Assert.Single(token.Audiences));
    }

    [Fact]
    public void Token_expires_after_the_configured_window()
    {
        var token = Read(ServiceWith(("Jwt:ExpiryMinutes", "5")).GenerateToken(User()));

        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddMinutes(4), DateTime.UtcNow.AddMinutes(6));
    }

    [Fact]
    public void Expiry_falls_back_to_an_hour_when_unconfigured()
    {
        var token = Read(ServiceWith(("Jwt:ExpiryMinutes", null!)).GenerateToken(User()));

        Assert.InRange(
            token.ValidTo,
            DateTime.UtcNow.AddMinutes(59),
            DateTime.UtcNow.AddMinutes(61)
        );
    }

    [Fact]
    public void A_token_is_accepted_by_the_settings_the_services_validate_with()
    {
        var token = ServiceWith().GenerateToken(User());

        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token,
            ServiceValidationParameters(),
            out _
        );

        Assert.True(principal.Identity!.IsAuthenticated);
    }

    [Fact]
    public void A_token_signed_with_a_different_secret_is_rejected()
    {
        var foreign = ServiceWith(("Jwt:Secret", "a-completely-different-signing-key-32-chars"))
            .GenerateToken(User());

        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                foreign,
                ServiceValidationParameters(),
                out _
            )
        );
    }

    [Fact]
    public void A_token_from_another_issuer_is_rejected()
    {
        var foreign = ServiceWith(("Jwt:Issuer", "somebody-else")).GenerateToken(User());

        Assert.Throws<SecurityTokenInvalidIssuerException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                foreign,
                ServiceValidationParameters(),
                out _
            )
        );
    }

    [Fact]
    public void A_token_meant_for_another_audience_is_rejected()
    {
        var foreign = ServiceWith(("Jwt:Audience", "some-other-api")).GenerateToken(User());

        Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                foreign,
                ServiceValidationParameters(),
                out _
            )
        );
    }

    [Fact]
    public void An_expired_token_is_rejected()
    {
        var expired = ServiceWith(("Jwt:ExpiryMinutes", "-1")).GenerateToken(User());

        Assert.Throws<SecurityTokenExpiredException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                expired,
                ServiceValidationParameters(),
                out _
            )
        );
    }

    [Fact]
    public void The_services_read_back_the_same_user_id_that_identity_signed()
    {
        // The whole cross-service contract in one assertion: Identity puts the id in "sub",
        // the services validate with MapInboundClaims = false so it stays "sub", and
        // CurrentUser reads it from there. Any one of those three drifting turns every
        // authorized request into a silent 401.
        var userId = Guid.NewGuid();
        var token = ServiceWith().GenerateToken(User(userId));

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear(); // what MapInboundClaims = false does
        var principal = handler.ValidateToken(token, ServiceValidationParameters(), out _);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext { User = principal });

        Assert.Equal(userId, new CurrentUser(accessor).UserId);
    }
}
