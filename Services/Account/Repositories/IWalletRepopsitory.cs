using Account.DTOs;

namespace Account.Repositories;

public interface IWalletRepository
{
    Task<UpdateWalletDto?> Deposit(Guid accId, decimal amount);
    Task<UpdateWalletDto?> Withdraw(Guid accId, decimal amount);
    Task<WalletBalanceDto?> GetBalance(Guid accId);
}
