using Account.Data;
using Account.DTOs;

namespace Account.Repositories;

public class WalletRepository(WalletContext db) : IWalletRepository
{
    public async Task<UpdateWalletDto?> Deposit(Guid accId, decimal amount)
    {
        var wallet = await db.Wallets.FindAsync(accId);
        if (wallet is null) return null;

        wallet.Amount += amount;
        await db.SaveChangesAsync();

        return new UpdateWalletDto(wallet.AccId, wallet.Amount);
    }
}