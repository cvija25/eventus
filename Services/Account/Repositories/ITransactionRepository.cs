using Account.DTOs;

namespace Account.Repositories;

public interface ITransactionRepository
{
    public Task<bool> CreateTransaction(TransactionDTO transactionDto);
    public Task<List<TransactionDTO>> GetTransactionsForUser(Guid userId);
    public Task<List<TransactionDTO>> GetTransactionsForEvent(Guid eventId);
}
