using Account.Data;
using Account.DTOs;
using Account.Entities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Account.Repositories;

public class TransactionRepository(AccountDbContext context, IMapper mapper)
    : ITransactionRepository
{
    public async Task<bool> CreateTransaction(TransactionDTO transactionDto)
    {
        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            EventId = transactionDto.EventId,
            AccountId = transactionDto.UserId,
            Outcome = transactionDto.Outcome,
            ShareAmount = transactionDto.ShareAmount,
            Type = transactionDto.Type,
        };
        context.Transactions.Add(transaction);
        var rows = await context.SaveChangesAsync();
        return rows > 0;
    }

    public async Task<List<TransactionDTO>> GetTransactionsForUser(Guid userId)
    {
        var transactions = await context
            .Transactions.Where(t => t.AccountId == userId)
            .ToListAsync();
        return mapper.Map<List<TransactionDTO>>(transactions);
    }

    public async Task<List<TransactionDTO>> GetTransactionsForEvent(Guid eventId)
    {
        var transactions = await context
            .Transactions.Where(t => t.EventId == eventId)
            .ToListAsync();
        return mapper.Map<List<TransactionDTO>>(transactions);
    }
}
