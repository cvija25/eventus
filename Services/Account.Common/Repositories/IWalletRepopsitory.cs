using Account.Common.DTOs;

namespace Account.Common.Repositories;

public interface IWalletRepository
{
    Task<UpdateWalletDto?> Deposit(Guid accId, decimal amount);
}