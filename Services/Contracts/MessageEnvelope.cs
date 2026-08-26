using System.Text.Json;

namespace Contracts;

public sealed record MessageEnvelope(Guid MessageId, string Type, string Payload)
{
    public static MessageEnvelope Create<T>(string type, T message) =>
        new(Guid.NewGuid(), type, JsonSerializer.Serialize(message));

    public T Deserialize<T>() =>
        JsonSerializer.Deserialize<T>(Payload)
        ?? throw new JsonException($"Envelope payload for '{Type}' is invalid.");
}

public static class MessageTypes
{
    public const string BetPlaced = "bet-placed";
    public const string SellShares = "sell-shares";
    public const string BetApproved = "bet-approved";
    public const string SellSharesApproved = "sell-shares-approved";
}
