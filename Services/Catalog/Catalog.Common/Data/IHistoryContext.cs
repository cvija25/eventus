using Catalog.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Common.Data;

public interface IHistoryContext
{
    DbSet<PriceHistory> Histories { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
