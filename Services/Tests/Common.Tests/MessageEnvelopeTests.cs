using System.Text.Json;
using Common.Enums;
using Common.Messaging;

namespace Common.Tests;

public class MessageEnvelopeTests
{
    [Fact]
    public void Create_round_trips_the_payload()
    {
        var original = new BetPlacedEvent
        {
            EventId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Stake = 12.5m,
            Outcome = MarketOutcome.No,
        };

        var envelope = MessageEnvelope.Create(MessageTypes.BetPlaced, original);
        var restored = envelope.Deserialize<BetPlacedEvent>();

        Assert.Equal(MessageTypes.BetPlaced, envelope.Type);
        Assert.NotEqual(Guid.Empty, envelope.MessageId);
        Assert.Equal(original.EventId, restored.EventId);
        Assert.Equal(original.OwnerId, restored.OwnerId);
        Assert.Equal(original.Stake, restored.Stake);
        Assert.Equal(original.Outcome, restored.Outcome);
    }

    [Fact]
    public void Create_gives_every_envelope_a_distinct_id()
    {
        var first = MessageEnvelope.Create(MessageTypes.BetPlaced, new BetPlacedEvent());
        var second = MessageEnvelope.Create(MessageTypes.BetPlaced, new BetPlacedEvent());

        Assert.NotEqual(first.MessageId, second.MessageId);
    }

    [Fact]
    public void Envelope_survives_transport_as_json()
    {
        // The consumers deserialize the envelope itself off the wire before unwrapping the
        // payload, so the outer record has to round-trip through System.Text.Json too.
        var envelope = MessageEnvelope.Create(
            MessageTypes.EventResolved,
            new EventResolvedEvent { EventId = Guid.NewGuid(), Outcome = MarketOutcome.Yes }
        );

        var onTheWire = JsonSerializer.Serialize(envelope);
        var received = JsonSerializer.Deserialize<MessageEnvelope>(onTheWire);

        Assert.NotNull(received);
        Assert.Equal(envelope, received);
        Assert.Equal(MarketOutcome.Yes, received.Deserialize<EventResolvedEvent>().Outcome);
    }

    [Fact]
    public void Deserialize_throws_when_the_payload_is_json_null()
    {
        var envelope = new MessageEnvelope(Guid.NewGuid(), MessageTypes.BetPlaced, "null");

        var ex = Assert.Throws<JsonException>(() => envelope.Deserialize<BetPlacedEvent>());
        Assert.Contains(MessageTypes.BetPlaced, ex.Message);
    }

    [Fact]
    public void Deserialize_throws_when_the_payload_is_not_json()
    {
        var envelope = new MessageEnvelope(Guid.NewGuid(), MessageTypes.BetPlaced, "not json");

        Assert.Throws<JsonException>(() => envelope.Deserialize<BetPlacedEvent>());
    }
}
