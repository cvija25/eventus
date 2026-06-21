using Account.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace Account.Common.Data;

public class WalletContext(DbContextOptions<WalletContext> options) : DbContext(options)
{
    public DbSet<Wallet> Wallets => Set<Wallet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Wallet>(e =>
        {
            e.ToTable("wallets");
            e.HasKey(w => w.AccId);
            e.Property(w => w.AccId).HasColumnName("acc_id");
            e.Property(w => w.Amount).HasColumnName("amount");
        });
    }
}