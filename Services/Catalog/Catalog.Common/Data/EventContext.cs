using Catalog.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Common.Data;

public class EventContext(DbContextOptions<EventContext> options)
    : DbContext(options),
        IEventContext
{
    public DbSet<Event> Events { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Event>()
            .Property(e => e.PotSizeYes)
            .HasDefaultValue(1m);

        modelBuilder.Entity<Event>()
            .Property(e => e.PotSizeNo)
            .HasDefaultValue(1m);
    }
}
