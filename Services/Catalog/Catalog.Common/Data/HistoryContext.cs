using Catalog.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Common.Data;

public class HistoryContext(DbContextOptions<HistoryContext> options)
    : DbContext(options),
        IHistoryContext
{
    public DbSet<PriceHistory> Histories { get; }
}
