using Account.Entities;
using Microsoft.EntityFrameworkCore;

namespace Account.Data;

public class WalletContext(DbContextOptions<WalletContext> options) : DbContext(options)
{
    public DbSet<Wallet> Wallets { get; set; }
}
