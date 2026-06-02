using Catalog.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Common.Data;

public class EventContext(DbContextOptions<EventContext> options)
    : DbContext(options),
        IEventContext
{
    public DbSet<Event> Events { get; set; }
}
