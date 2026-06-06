using Catalog.Common.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Catalog.Common.Extensions;

public static class HostExtensions
{
    public static async Task MigrateCatalogDatabase(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<EventContext>>();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                return;
            }
            catch (Exception ex) when (attempt < 5)
            {
                logger.LogWarning(ex, "Migration attempt {Attempt} failed, retrying in {Delay}s", attempt, attempt * 2);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
        }
    }
}
