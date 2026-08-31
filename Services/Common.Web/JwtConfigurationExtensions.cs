using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Common.Web;

public static class JwtConfigurationExtensions
{
    public static JwtOptions GetJwtOptions(this IConfiguration configuration)
    {
        var secret = configuration.Require($"{JwtOptions.SectionName}:Secret");
        var bytes = Encoding.UTF8.GetByteCount(secret);
        if (bytes < JwtOptions.MinimumSecretBytes)
            throw new InvalidOperationException(
                $"Jwt:Secret is {bytes} bytes; HS256 needs at least "
                    + $"{JwtOptions.MinimumSecretBytes}. Use a longer key."
            );

        var expiry = configuration[$"{JwtOptions.SectionName}:ExpiryMinutes"];

        return new JwtOptions
        {
            Secret = secret,
            Issuer = configuration.Require($"{JwtOptions.SectionName}:Issuer"),
            Audience = configuration.Require($"{JwtOptions.SectionName}:Audience"),
            ExpiryMinutes = int.TryParse(expiry, out var minutes) ? minutes : 60,
        };
    }

    public static IServiceCollection AddEventusJwtAuth(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var jwt = configuration.GetJwtOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };
            });
        services.AddAuthorization();

        return services;
    }
}
