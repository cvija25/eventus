using Account.DTOs;

namespace Account.Repositories;

public interface IWalletRepository
{
    Task<UpdateWalletDto?> Deposit(Guid accId, decimal amount);
}