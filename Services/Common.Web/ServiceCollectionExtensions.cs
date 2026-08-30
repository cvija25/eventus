using Microsoft.Extensions.DependencyInjection;

namespace Common.Web;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        return services;
    }
}
