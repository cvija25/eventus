using Account.Data;
using Account.DTOs;
using Account.Entities;

namespace Account.Repositories;

public class WalletRepository(AccountDbContext db) : IWalletRepository
{
    public async Task<UpdateWalletDto?> Deposit(Guid accId, decimal amount)
    {
        var wallet = await db.Wallets.FindAsync(accId);
        if (wallet is null)
            return null;

        wallet.AvailableFunds += amount;
        await db.SaveChangesAsync();

        return new UpdateWalletDto(wallet.AccountId, wallet.AvailableFunds);
    }

    public async Task<UpdateWalletDto?> Withdraw(Guid accId, decimal amount)
    {
        var wallet = await db.Wallets.FindAsync(accId);
        if (wallet is null)
            return null;

        wallet.AvailableFunds = Math.Max(wallet.AvailableFunds - amount, 0);
        await db.SaveChangesAsync();

        return new UpdateWalletDto(wallet.AccountId, wallet.AvailableFunds);
    }

    public async Task<UpdateWalletDto?> DepositReserveFund(Guid accId, decimal amount)
    {
        var wallet = await db.Wallets.FindAsync(accId);
        if (wallet is null)
            return null;

        wallet.ReserveFunds += amount;
        await db.SaveChangesAsync();

        return new UpdateWalletDto(wallet.AccountId, wallet.ReserveFunds);
    }

    public async Task<UpdateWalletDto?> WithdrawReserveFund(Guid accId, decimal amount)
    {
        var wallet = await db.Wallets.FindAsync(accId);
        if (wallet is null)
            return null;

        wallet.ReserveFunds = Math.Max(wallet.ReserveFunds - amount, 0);
        await db.SaveChangesAsync();
        return new UpdateWalletDto(wallet.AccountId, wallet.ReserveFunds);
    }

    public async Task<WalletBalanceDto?> GetBalance(Guid accId)
    {
        var wallet = await db.Wallets.FindAsync(accId);
        if (wallet is null)
            return null;

        return new WalletBalanceDto(Math.Max(0, wallet.AvailableFunds - wallet.ReserveFunds));
    }

    public async Task CreateWalletIfMissing(Guid userId)
    {
        if (await db.Wallets.FindAsync(userId) != null)
            return;
        var wallet = new Wallet { AccountId = userId };
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();
    }
}
