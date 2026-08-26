using Contracts;

namespace Account.DTOs;

public record TransactionDTO(
    Guid EventId,
    Guid UserId,
    decimal ShareAmount,
    MarketOutcome Outcome,
    TransactionType Type
);
