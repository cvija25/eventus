using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Catalog.Common.Data;

public class EventContextFactory : IDesignTimeDbContextFactory<EventContext>
{
    public EventContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<EventContext>()
            .UseNpgsql(config.GetConnectionString("DefaultConnection"))
            .Options;

        return new EventContext(options);
    }
}
