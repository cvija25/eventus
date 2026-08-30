using Catalog.Common.Data;
using Catalog.Common.Mappings;
using Catalog.Common.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogCommon(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<EventContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("CatalogDb"))
        );
        services.AddDbContext<HistoryContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("CatalogDb"))
        );
        services.AddScoped<IHistoryContext>(sp => sp.GetRequiredService<HistoryContext>());
        services.AddScoped<IPriceHistoryRepository, PriceHistoryRepository>();
        services.AddScoped<IEventContext>(sp => sp.GetRequiredService<EventContext>());
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddAutoMapper(cfg => cfg.AddProfile<EventMappingProfile>());

        return services;
    }
}
