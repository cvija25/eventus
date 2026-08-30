using Common.Enums;

namespace Common.Messaging;

public class EventResolvedEvent
{
    public Guid EventId { get; set; }
    public MarketOutcome Outcome { get; set; }
}
