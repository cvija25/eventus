using System.Text.Json;

namespace Common.Messaging;

public sealed record MessageEnvelope(Guid MessageId, string Type, string Payload)
{
    public static MessageEnvelope Create<T>(string type, T message)
    {
        return new MessageEnvelope(Guid.NewGuid(), type, JsonSerializer.Serialize(message));
    }

    public T Deserialize<T>()
    {
        return JsonSerializer.Deserialize<T>(Payload)
            ?? throw new JsonException($"Envelope payload for '{Type}' is invalid.");
    }
}

public static class MessageTypes
{
    public const string BetPlaced = "bet-placed";
    public const string SellShares = "sell-shares";
    public const string BetApproved = "bet-approved";
    public const string SellSharesApproved = "sell-shares-approved";
    public const string PriceUpdate = "price-update";
}
